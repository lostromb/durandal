using Durandal.API;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("Language Understanding")]
[assembly: AssemblyDescription("Standalone LU server for Durandal")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Microsoft Corporation")]
[assembly: AssemblyProduct("Durandal")]
[assembly: AssemblyCopyright("Copyright © 2016 Microsoft Corporation")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("739dde5f-f5c2-4bed-8f68-8288e7dfb133")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion(SVNVersionInfo.AssemblyVersion)]
[assembly: AssemblyFileVersion(SVNVersionInfo.AssemblyVersion)]

// Do not perform runtime string interning because we will manager our own pools of compressed memory for strings
[assembly: CompilationRelaxations(CompilationRelaxations.NoStringInterning)]