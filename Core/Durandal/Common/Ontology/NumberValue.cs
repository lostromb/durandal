using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Durandal.Common.Ontology
{
    /// <summary>
    /// An adapter that reads/writes number triples from a knowledge context
    /// </summary>
    public class NumberValue
    {
        private readonly KnowledgeContext _context;
        private readonly string _entityId;
        private readonly string _fieldName;

        /// <summary>
        /// Constructs a new NumberValue
        /// </summary>
        /// <param name="context"></param>
        /// <param name="entityId"></param>
        /// <param name="fieldName"></param>
        public NumberValue(KnowledgeContext context, string entityId, string fieldName)
        {
            _context = context;
            _entityId = entityId;
            _fieldName = fieldName;
        }

        /// <summary>
        /// Gets or sets the value of this triple. If there is no value, this returns null.
        /// If a value already exists when performing a set, that value will be overwritten.
        /// If there is more than one value, this will return a single one arbitrarily.
        /// </summary>
        public decimal? Value
        {
            get
            {
                IEnumerator<decimal> enumerator = List.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    return enumerator.Current;
                }

                return null;
            }
            set
            {
                if (value.HasValue)
                {
                    _context.Overwrite(new Triple(_entityId, _fieldName, value.Value));
                }
                else
                {
                    _context.Disassociate(_entityId, _fieldName);
                }
            }
        }

        /// <summary>
        /// Retrieves the list of all values set for this property.
        /// </summary>
        public IList<decimal> List
        {
            get
            {
                IList<decimal> returnVal = new List<decimal>();
                foreach (Triple value in _context.GetPropertiesOf(_entityId, _fieldName))
                {
                    if (value.Value.Type == PrimitiveType.Number)
                    {
                        returnVal.Add(value.Value.ValueAsNumber);
                    }
                }

                return returnVal;
            }
        }

        /// <summary>
        /// Adds a value to the list of possible values of this property.
        /// </summary>
        /// <param name="value">The value to add</param>
        public void Add(decimal value)
        {
            _context.Associate(new Triple(_entityId, _fieldName, value));
        }
    }
}
