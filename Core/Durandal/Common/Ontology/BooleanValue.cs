using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Durandal.Common.Ontology
{
    /// <summary>
    /// An adapter that reads/writes boolean triples from a knowledge context
    /// </summary>
    public class BooleanValue
    {
        private readonly KnowledgeContext _context;
        private readonly string _entityId;
        private readonly string _fieldName;
        
        /// <summary>
        /// Constructs a new BooleanValue
        /// </summary>
        /// <param name="context"></param>
        /// <param name="entityId"></param>
        /// <param name="fieldName"></param>
        public BooleanValue(KnowledgeContext context, string entityId, string fieldName)
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
        public bool? Value
        {
            get
            {
                IEnumerator<bool> enumerator = List.GetEnumerator();
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
        /// Enumerating a list of boolean usually makes no sense but is provided for completeness
        /// </summary>
        public IList<bool> List
        {
            get
            {
                IList<bool> returnVal = new List<bool>();
                foreach (Triple value in _context.GetPropertiesOf(_entityId, _fieldName))
                {
                    if (value.Value.Type == PrimitiveType.Boolean)
                    {
                        returnVal.Add(value.Value.ValueAsBoolean);
                    }
                }

                return returnVal;
            }
        }

        /// <summary>
        /// Adds a value to the list of possible values of this property.
        /// A list of booleans usually makes no sense but is provided for completeness
        /// </summary>
        /// <param name="value">The value to add</param>
        public void Add(bool value)
        {
            _context.Associate(new Triple(_entityId, _fieldName, value));
        }
    }
}
