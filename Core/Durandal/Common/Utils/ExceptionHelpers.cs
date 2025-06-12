using Durandal.Common.IO;
using System;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Durandal.Common.Utils
{
    /// <summary>
    /// Extension classes for Exception objects, primarily to help print their details to string builders or other buffers
    /// which is a very common case for things like logging
    /// </summary>
    public static class ExceptionHelpers
    {
        /// <summary>
        /// Creates a detailed exception message for this exception, including full stack traces for nested inner exceptions, and returns it as a multiline string
        /// </summary>
        /// <param name="e">The exception to get details for</param>
        /// <returns>A multiline string containing exception details</returns>
        public static string GetDetailedMessage(this Exception e)
        {
            e.AssertNonNull(nameof(e));
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                PrintToStringBuilderDetailed(e, pooledSb.Builder);
                return pooledSb.Builder.ToString();
            }
        }

        /// <summary>
        /// Does the equivalent of stringBuilder.Append(e), but with much better allocation behavior.
        /// </summary>
        /// <param name="e">The exception to get details for.</param>
        /// <param name="stringBuilder">The string builder to append the exception to.</param>
        public static void PrintToStringBuilder(this Exception e, StringBuilder stringBuilder)
        {
            e.AssertNonNull(nameof(e));
            ExceptionToStringBuilder(e, stringBuilder);
        }

        /// <summary>
        /// Creates a detailed exception message for this exception, including full stack traces for nested inner exceptions, and appends it to the given string builder
        /// </summary>
        /// <param name="e">The exception to get details for</param>
        /// <param name="stringBuilder">The string builder to append to</param>
        public static void PrintToStringBuilderDetailed(this Exception e, StringBuilder stringBuilder)
        {
            e.AssertNonNull(nameof(e));
            stringBuilder.AssertNonNull(nameof(stringBuilder));
            // Unwind the stack of inner exceptions to a max depth of 4
            using (PooledBuffer<Exception> nestedExceptions = BufferPool<Exception>.Rent(4))
            {
                nestedExceptions.Buffer[0] = e;
                Exception inner = e.InnerException;
                int nestCount = 1;

                while (inner != null && nestCount < nestedExceptions.Length)
                {
                    nestedExceptions.Buffer[nestCount++] = inner;
                    inner = inner.InnerException;
                }

                // Now iterate through in reverse order, starting at the innermost exception,
                // and log each exception's message and stack trace
                for (int c = nestCount - 1; c >= 0; c--)
                {
                    if (c < nestCount - 1)
                    {
                        stringBuilder.AppendLine();
                    }

                    ExceptionToStringBuilder(nestedExceptions.Buffer[c], stringBuilder);
                }
            }
        }

#if !NET6_0_OR_GREATER
        /// <summary>
        /// Performs the equivalent of stringBuilder.Append(ex.StackTrace), but with better allocation performance.
        /// </summary>
        /// <param name="exception">The exception to print</param>
        /// <param name="stringBuilder">The string builder to append to</param>
        public static void PrintStackTraceToStringBuilder(this Exception exception, StringBuilder stringBuilder)
        {
            exception.AssertNonNull(nameof(exception));
            stringBuilder.AssertNonNull(nameof(stringBuilder));
            if (exception.StackTrace != null)
            {
                stringBuilder.Append(exception.StackTrace);
            }
        }

        private static void ExceptionToStringBuilder(Exception ex, StringBuilder sb)
        {
            sb.Append(ex.ToString());
        }
#endif

#if NET6_0_OR_GREATER
        /// <summary>
        /// Performs the equivalent of stringBuilder.Append(ex.StackTrace), but with better allocation performance.
        /// </summary>
        /// <param name="exception">The exception to print</param>
        /// <param name="stringBuilder">The string builder to append to</param>
        public static void PrintStackTraceToStringBuilder(this Exception exception, StringBuilder stringBuilder)
        {
            exception.AssertNonNull(nameof(exception));
            stringBuilder.AssertNonNull(nameof(stringBuilder));
            StackTrace stackTrace = new StackTrace(exception, fNeedFileInfo: true);
            if (stackTrace != null)
            {
                StackTraceToStringBuilder(stackTrace, stringBuilder);
            }
        }

        // Below code ripped out of the CLR and modified slightly for our needs
        private static void ExceptionToStringBuilder(Exception ex, StringBuilder sb)
        {
            StackTrace stackTrace = new StackTrace(ex, fNeedFileInfo: true);

            sb.Append(ex.GetType());
            if (!string.IsNullOrEmpty(ex.Message))
            {
                sb.Append(": ");
                sb.Append(ex.Message);
            }
            if (ex.InnerException != null)
            {
                sb.AppendLine();
                sb.Append(" ---> ");
                ExceptionToStringBuilder(ex.InnerException, sb);
                sb.AppendLine();
                sb.Append("   ");
                sb.Append("--- End of inner exception stack trace ---");
            }
            if (stackTrace != null)
            {
                sb.AppendLine();
                StackTraceToStringBuilder(stackTrace, sb);
            }
        }

        /// <summary>
        /// Builds a readable representation of the stack trace, appending it to the given string builder
        /// </summary>
        private static void StackTraceToStringBuilder(StackTrace trace, StringBuilder sb)
        {
            const string word_At = "at";
            bool fFirstFrame = true;
            for (int iFrameIndex = 0; iFrameIndex < trace.FrameCount; iFrameIndex++)
            {
                StackFrame sf = trace.GetFrame(iFrameIndex);
                MethodBase mb = sf?.GetMethod();
                if (mb != null && (ShowInStackTrace(mb) ||
                                   (iFrameIndex == trace.FrameCount - 1))) // Don't filter last frame
                {
                    // We want a newline at the end of every line except for the last
                    if (fFirstFrame)
                        fFirstFrame = false;
                    else
                        sb.AppendLine();

                    sb.Append("   ").Append(word_At).Append(' ');

                    bool isAsync = false;
                    Type declaringType = mb.DeclaringType;
                    string methodName = mb.Name;
                    bool methodChanged = false;
                    if (declaringType != null && declaringType.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
                    {
                        isAsync = declaringType.IsAssignableTo(typeof(IAsyncStateMachine));
                        if (isAsync || declaringType.IsAssignableTo(typeof(IEnumerator)))
                        {
                            methodChanged = TryResolveStateMachineMethod(ref mb, out declaringType);
                        }
                    }

                    // if there is a type (non global method) print it
                    // ResolveStateMachineMethod may have set declaringType to null
                    if (declaringType != null)
                    {
                        // Append t.FullName, replacing '+' with '.'
                        string fullName = declaringType.FullName!;
                        for (int i = 0; i < fullName.Length; i++)
                        {
                            char ch = fullName[i];
                            sb.Append(ch == '+' ? '.' : ch);
                        }
                        sb.Append('.');
                    }
                    sb.Append(mb.Name);

                    // deal with the generic portion of the method
                    if (mb is MethodInfo mi && mi.IsGenericMethod)
                    {
                        Type[] typars = mi.GetGenericArguments();
                        sb.Append('[');
                        int k = 0;
                        bool fFirstTyParam = true;
                        while (k < typars.Length)
                        {
                            if (!fFirstTyParam)
                                sb.Append(',');
                            else
                                fFirstTyParam = false;

                            sb.Append(typars[k].Name);
                            k++;
                        }
                        sb.Append(']');
                    }

                    ParameterInfo[] pi = null;
                    try
                    {
                        pi = mb.GetParameters();
                    }
                    catch
                    {
                        // The parameter info cannot be loaded, so we don't
                        // append the parameter list.
                    }
                    if (pi != null)
                    {
                        // arguments printing
                        sb.Append('(');
                        bool fFirstParam = true;
                        for (int j = 0; j < pi.Length; j++)
                        {
                            if (!fFirstParam)
                                sb.Append(", ");
                            else
                                fFirstParam = false;

                            string typeName = "<UnknownType>";
                            if (pi[j].ParameterType != null)
                                typeName = pi[j].ParameterType.Name;
                            sb.Append(typeName);
                            string parameterName = pi[j].Name;
                            if (parameterName != null)
                            {
                                sb.Append(' ');
                                sb.Append(parameterName);
                            }
                        }
                        sb.Append(')');
                    }

                    if (methodChanged)
                    {
                        // Append original method name e.g. +MoveNext()
                        sb.Append('+');
                        sb.Append(methodName);
                        sb.Append('(').Append(')');
                    }

                    // source location printing
                    if (sf!.GetILOffset() != -1)
                    {
                        // If we don't have a PDB or PDB-reading is disabled for the module,
                        // then the file name will be null.
                        string fileName = sf.GetFileName();

                        if (fileName != null)
                        {
                            // tack on " in c:\tmp\MyFile.cs:line 5"
                            sb.Append(' ');
                            //const string inFileLineNum = "in {0}:line {1}";
                            //sb.AppendFormat(CultureInfo.InvariantCulture, inFileLineNum, fileName, sf.GetFileLineNumber());
                            sb.Append("in ");
                            sb.Append(fileName);
                            sb.Append(":line ");
                            sb.Append(sf.GetFileLineNumber());
                        }
                        else if (mb.ReflectedType != null)
                        {
                            string assemblyName = mb.ReflectedType.Module.ScopeName;
                            try
                            {
                                int token = mb.MetadataToken;
                                sb.Append(' ');
                                const string inFileILOffset = "in {0}:token 0x{1:x}+0x{2:x}";
                                sb.AppendFormat(CultureInfo.InvariantCulture, inFileILOffset, assemblyName, token, sf.GetILOffset());
                            }
                            catch (System.InvalidOperationException) { }
                        }
                    }
                }
            }
        }

        private static bool ShowInStackTrace(MethodBase mb)
        {
            Debug.Assert(mb != null);

            if ((mb.MethodImplementationFlags & MethodImplAttributes.AggressiveInlining) != 0)
            {
                // Aggressive Inlines won't normally show in the StackTrace; however for Tier0 Jit and
                // cross-assembly AoT/R2R these inlines will be blocked until Tier1 Jit re-Jits
                // them when they will inline. We don't show them in the StackTrace to bring consistency
                // between this first-pass asm and fully optimized asm.
                return false;
            }

            try
            {
                if (mb.IsDefined(typeof(StackTraceHiddenAttribute), inherit: false))
                {
                    // Don't show where StackTraceHidden is applied to the method.
                    return false;
                }

                Type declaringType = mb.DeclaringType;
                // Methods don't always have containing types, for example dynamic RefEmit generated methods.
                if (declaringType != null &&
                    declaringType.IsDefined(typeof(StackTraceHiddenAttribute), inherit: false))
                {
                    // Don't show where StackTraceHidden is applied to the containing Type of the method.
                    return false;
                }
            }
            catch
            {
                // Getting the StackTraceHiddenAttribute has failed, behave as if it was not present.
                // One of the reasons can be that the method mb or its declaring type use attributes
                // defined in an assembly that is missing.
            }

            return true;
        }

        private static bool TryResolveStateMachineMethod(ref MethodBase method, out Type declaringType)
        {
            Debug.Assert(method != null);
            Debug.Assert(method.DeclaringType != null);

            declaringType = method.DeclaringType;

            Type parentType = declaringType.DeclaringType;
            if (parentType == null)
            {
                return false;
            }

            [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2070:UnrecognizedReflectionPattern",
                Justification = "Using Reflection to find the state machine's corresponding method is safe because the corresponding method is the only " +
                                "caller of the state machine. If the state machine is present, the corresponding method will be, too.")]
            static MethodInfo[] GetDeclaredMethods(Type type) =>
                type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            MethodInfo[] methods = GetDeclaredMethods(parentType);
            if (methods == null)
            {
                return false;
            }

            foreach (MethodInfo candidateMethod in methods)
            {
                StateMachineAttribute[] attributes = (StateMachineAttribute[])Attribute.GetCustomAttributes(candidateMethod, typeof(StateMachineAttribute), inherit: false);
                if (attributes == null)
                {
                    continue;
                }

                bool foundAttribute = false, foundIteratorAttribute = false;
                foreach (StateMachineAttribute asma in attributes)
                {
                    if (asma.StateMachineType == declaringType)
                    {
                        foundAttribute = true;
                        foundIteratorAttribute |= asma is IteratorStateMachineAttribute || asma is AsyncIteratorStateMachineAttribute;
                    }
                }

                if (foundAttribute)
                {
                    // If this is an iterator (sync or async), mark the iterator as changed, so it gets the + annotation
                    // of the original method. Non-iterator async state machines resolve directly to their builder methods
                    // so aren't marked as changed.
                    method = candidateMethod;
                    declaringType = candidateMethod.DeclaringType!;
                    return foundIteratorAttribute;
                }
            }

            return false;
        }
#endif //NET6_0_OR_GREATER
    }
}
