namespace Durandal.Common.Collections
{
    using Durandal.Common.Utils;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Boilerplate adapter to cast the result of an IEnumerator to another type.
    /// </summary>
    /// <typeparam name="TIn">The type of objects the original enumerator returns.</typeparam>
    /// <typeparam name="TOut">The type of objects that this enumerator returns.</typeparam>
    public class CastingEnumerator<TIn, TOut> : IEnumerator<TOut>
    {
        private readonly IEnumerator<TIn> internalEnumerator;
        private readonly Func<TIn, TOut> castFunc;

        /// <summary>
        /// Constructs a new instance of <see cref="CastingEnumerator{TIn, TOut}"/>.
        /// </summary>
        /// <param name="internalEnumerator">The enumerator being wrapped.</param>
        /// <param name="castFunc">A method to cast the underlying enumerated type to the exposed <typeparamref name="TOut"/></param>
        public CastingEnumerator(IEnumerator<TIn> internalEnumerator, Func<TIn, TOut> castFunc)
        {
            this.internalEnumerator = internalEnumerator.AssertNonNull(nameof(internalEnumerator));
            this.castFunc = castFunc.AssertNonNull(nameof(castFunc));
        }

        /// <inheritdoc />
        public TOut Current
        {
            get; private set;
        }

        /// <inheritdoc />
        object IEnumerator.Current => Current;

        /// <inheritdoc />
        public void Dispose()
        {
            internalEnumerator.Dispose();
        }

        /// <inheritdoc />
        public bool MoveNext()
        {
            if (this.internalEnumerator.MoveNext())
            {
                this.Current = this.castFunc(this.internalEnumerator.Current);
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public void Reset()
        {
            this.internalEnumerator.Reset();
            this.Current = default(TOut);
        }
    }
}
