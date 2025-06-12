using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Ontology;
using Durandal.Tests.TestSchemas;

namespace Durandal.Tests.TestSchemas
{
    /// <summary>
    /// <para>QuantitativeValue</para>
    /// <para>A point value or interval for product characteristics and other purposes.</para>
    /// </summary>
    public class QuantitativeValue : Entity
    {
        private static HashSet<string> _inheritance = new HashSet<string>() { "http://schema.org/StructuredValue", "http://schema.org/Intangible", "http://schema.org/Thing" };

        public QuantitativeValue(KnowledgeContext context, string entityId = null) : base(context, "http://schema.org/QuantitativeValue", entityId) { Initialize(); }

        /// <summary>
        /// Casting constructor
        /// </summary>
        /// <param name="castFrom">The entity that this one is being cast from</param>
        public QuantitativeValue(Entity castFrom) : base(castFrom, "http://schema.org/QuantitativeValue") { Initialize(); }

        protected override ISet<string> InheritsFromInternal
        {
            get
            {
                return _inheritance;
            }
        }

        private void Initialize()
        {
            UnitCode_as_URL = new IdentifierValue<Entity>(_context, EntityId, "unitCode");
            UnitCode_as_string = new TextValue(_context, EntityId, "unitCode");
            MinValue = new NumberValue(_context, EntityId, "minValue");
            Value_as_StructuredValue = new IdentifierValue<StructuredValue>(_context, EntityId, "value");
            Value_as_number = new NumberValue(_context, EntityId, "value");
            Value_as_bool = new BooleanValue(_context, EntityId, "value");
            Value_as_string = new TextValue(_context, EntityId, "value");
            AdditionalProperty = new IdentifierValue<Entity>(_context, EntityId, "additionalProperty");
            ValueReference_as_Enumeration = new IdentifierValue<Enumeration>(_context, EntityId, "valueReference");
            ValueReference_as_PropertyValue = new IdentifierValue<Entity>(_context, EntityId, "valueReference");
            ValueReference_as_StructuredValue = new IdentifierValue<StructuredValue>(_context, EntityId, "valueReference");
            ValueReference_as_QuantitativeValue = new IdentifierValue<QuantitativeValue>(_context, EntityId, "valueReference");
            ValueReference_as_QualitativeValue = new IdentifierValue<Entity>(_context, EntityId, "valueReference");
            MaxValue = new NumberValue(_context, EntityId, "maxValue");
            UnitText = new TextValue(_context, EntityId, "unitText");
            SameAs = new IdentifierValue<Entity>(_context, EntityId, "sameAs");
            Url = new IdentifierValue<Entity>(_context, EntityId, "url");
            Image_as_ImageObject = new IdentifierValue<Entity>(_context, EntityId, "image");
            Image_as_URL = new IdentifierValue<Entity>(_context, EntityId, "image");
            AdditionalType = new IdentifierValue<Entity>(_context, EntityId, "additionalType");
            Name = new TextValue(_context, EntityId, "name");
            Identifier_as_PropertyValue = new IdentifierValue<Entity>(_context, EntityId, "identifier");
            Identifier_as_string = new TextValue(_context, EntityId, "identifier");
            Identifier_as_URL = new IdentifierValue<Entity>(_context, EntityId, "identifier");
            PotentialAction = new IdentifierValue<Entity>(_context, EntityId, "potentialAction");
            MainEntityOfPage_as_URL = new IdentifierValue<Entity>(_context, EntityId, "mainEntityOfPage");
            MainEntityOfPage_as_CreativeWork = new IdentifierValue<Entity>(_context, EntityId, "mainEntityOfPage");
            Description = new TextValue(_context, EntityId, "description");
            DisambiguatingDescription = new TextValue(_context, EntityId, "disambiguatingDescription");
            AlternateName = new TextValue(_context, EntityId, "alternateName");
        }

        /// <summary>
        /// <para>(From http://schema.org/QuantitativeValue)</para>
        /// <para>The unit of measurement given using the UN/CEFACT Common Code (3 characters) or a URL. Other codes than the UN/CEFACT Common Code may be used with a prefix followed by a colon.</para>
        /// </summary>
        public IdentifierValue<Entity> UnitCode_as_URL { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/QuantitativeValue)</para>
        /// <para>The unit of measurement given using the UN/CEFACT Common Code (3 characters) or a URL. Other codes than the UN/CEFACT Common Code may be used with a prefix followed by a colon.</para>
        /// </summary>
        public TextValue UnitCode_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/QuantitativeValue)</para>
        /// <para>The lower value of some characteristic or property.</para>
        /// </summary>
        public NumberValue MinValue { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/QuantitativeValue)</para>
        /// <para><p>The value of the quantitative value or property value node.</p></para>
        /// <para><ul></para>
        /// <para><li>For <a class="localLink" href="http://schema.org/QuantitativeValue">QuantitativeValue</a> and <a class="localLink" href="http://schema.org/MonetaryAmount">MonetaryAmount</a>, the recommended type for values is 'Number'.</li></para>
        /// <para><li>For <a class="localLink" href="http://schema.org/PropertyValue">PropertyValue</a>, it can be 'Text;', 'Number', 'Boolean', or 'StructuredValue'.</li></para>
        /// <para></ul></para>
        /// </summary>
        public IdentifierValue<StructuredValue> Value_as_StructuredValue { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/QuantitativeValue)</para>
        /// <para><p>The value of the quantitative value or property value node.</p></para>
        /// <para><ul></para>
        /// <para><li>For <a class="localLink" href="http://schema.org/QuantitativeValue">QuantitativeValue</a> and <a class="localLink" href="http://schema.org/MonetaryAmount">MonetaryAmount</a>, the recommended type for values is 'Number'.</li></para>
        /// <para><li>For <a class="localLink" href="http://schema.org/PropertyValue">PropertyValue</a>, it can be 'Text;', 'Number', 'Boolean', or 'StructuredValue'.</li></para>
        /// <para></ul></para>
        /// </summary>
        public NumberValue Value_as_number { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/QuantitativeValue)</para>
        /// <para><p>The value of the quantitative value or property value node.</p></para>
        /// <para><ul></para>
        /// <para><li>For <a class="localLink" href="http://schema.org/QuantitativeValue">QuantitativeValue</a> and <a class="localLink" href="http://schema.org/MonetaryAmount">MonetaryAmount</a>, the recommended type for values is 'Number'.</li></para>
        /// <para><li>For <a class="localLink" href="http://schema.org/PropertyValue">PropertyValue</a>, it can be 'Text;', 'Number', 'Boolean', or 'StructuredValue'.</li></para>
        /// <para></ul></para>
        /// </summary>
        public BooleanValue Value_as_bool { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/QuantitativeValue)</para>
        /// <para><p>The value of the quantitative value or property value node.</p></para>
        /// <para><ul></para>
        /// <para><li>For <a class="localLink" href="http://schema.org/QuantitativeValue">QuantitativeValue</a> and <a class="localLink" href="http://schema.org/MonetaryAmount">MonetaryAmount</a>, the recommended type for values is 'Number'.</li></para>
        /// <para><li>For <a class="localLink" href="http://schema.org/PropertyValue">PropertyValue</a>, it can be 'Text;', 'Number', 'Boolean', or 'StructuredValue'.</li></para>
        /// <para></ul></para>
        /// </summary>
        public TextValue Value_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/QuantitativeValue)</para>
        /// <para>A property-value pair representing an additional characteristics of the entitity, e.g. a product feature or another characteristic for which there is no matching property in schema.org.</p></para>
        /// <para><p>Note: Publishers should be aware that applications designed to use specific schema.org properties (e.g. http://schema.org/width, http://schema.org/color, http://schema.org/gtin13, ...) will typically expect such data to be provided using those properties, rather than using the generic property/value mechanism.</para>
        /// </summary>
        public IdentifierValue<Entity> AdditionalProperty { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/QuantitativeValue)</para>
        /// <para>A pointer to a secondary value that provides additional information on the original value, e.g. a reference temperature.</para>
        /// </summary>
        public IdentifierValue<Enumeration> ValueReference_as_Enumeration { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/QuantitativeValue)</para>
        /// <para>A pointer to a secondary value that provides additional information on the original value, e.g. a reference temperature.</para>
        /// </summary>
        public IdentifierValue<Entity> ValueReference_as_PropertyValue { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/QuantitativeValue)</para>
        /// <para>A pointer to a secondary value that provides additional information on the original value, e.g. a reference temperature.</para>
        /// </summary>
        public IdentifierValue<StructuredValue> ValueReference_as_StructuredValue { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/QuantitativeValue)</para>
        /// <para>A pointer to a secondary value that provides additional information on the original value, e.g. a reference temperature.</para>
        /// </summary>
        public IdentifierValue<QuantitativeValue> ValueReference_as_QuantitativeValue { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/QuantitativeValue)</para>
        /// <para>A pointer to a secondary value that provides additional information on the original value, e.g. a reference temperature.</para>
        /// </summary>
        public IdentifierValue<Entity> ValueReference_as_QualitativeValue { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/QuantitativeValue)</para>
        /// <para>The upper value of some characteristic or property.</para>
        /// </summary>
        public NumberValue MaxValue { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/QuantitativeValue)</para>
        /// <para>A string or text indicating the unit of measurement. Useful if you cannot provide a standard unit code for</para>
        /// <para><a href='unitCode'>unitCode</a>.</para>
        /// </summary>
        public TextValue UnitText { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>URL of a reference Web page that unambiguously indicates the item's identity. E.g. the URL of the item's Wikipedia page, Wikidata entry, or official website.</para>
        /// </summary>
        public IdentifierValue<Entity> SameAs { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>URL of the item.</para>
        /// </summary>
        public IdentifierValue<Entity> Url { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>An image of the item. This can be a <a class="localLink" href="http://schema.org/URL">URL</a> or a fully described <a class="localLink" href="http://schema.org/ImageObject">ImageObject</a>.</para>
        /// </summary>
        public IdentifierValue<Entity> Image_as_ImageObject { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>An image of the item. This can be a <a class="localLink" href="http://schema.org/URL">URL</a> or a fully described <a class="localLink" href="http://schema.org/ImageObject">ImageObject</a>.</para>
        /// </summary>
        public IdentifierValue<Entity> Image_as_URL { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>An additional type for the item, typically used for adding more specific types from external vocabularies in microdata syntax. This is a relationship between something and a class that the thing is in. In RDFa syntax, it is better to use the native RDFa syntax - the 'typeof' attribute - for multiple types. Schema.org tools may have only weaker understanding of extra types, in particular those defined externally.</para>
        /// </summary>
        public IdentifierValue<Entity> AdditionalType { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>The name of the item.</para>
        /// </summary>
        public TextValue Name { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>The identifier property represents any kind of identifier for any kind of <a class="localLink" href="http://schema.org/Thing">Thing</a>, such as ISBNs, GTIN codes, UUIDs etc. Schema.org provides dedicated properties for representing many of these, either as textual strings or as URL (URI) links. See <a href="/docs/datamodel.html#identifierBg">background notes</a> for more details.</para>
        /// </summary>
        public IdentifierValue<Entity> Identifier_as_PropertyValue { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>The identifier property represents any kind of identifier for any kind of <a class="localLink" href="http://schema.org/Thing">Thing</a>, such as ISBNs, GTIN codes, UUIDs etc. Schema.org provides dedicated properties for representing many of these, either as textual strings or as URL (URI) links. See <a href="/docs/datamodel.html#identifierBg">background notes</a> for more details.</para>
        /// </summary>
        public TextValue Identifier_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>The identifier property represents any kind of identifier for any kind of <a class="localLink" href="http://schema.org/Thing">Thing</a>, such as ISBNs, GTIN codes, UUIDs etc. Schema.org provides dedicated properties for representing many of these, either as textual strings or as URL (URI) links. See <a href="/docs/datamodel.html#identifierBg">background notes</a> for more details.</para>
        /// </summary>
        public IdentifierValue<Entity> Identifier_as_URL { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>Indicates a potential Action, which describes an idealized action in which this thing would play an 'object' role.</para>
        /// </summary>
        public IdentifierValue<Entity> PotentialAction { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>Indicates a page (or other CreativeWork) for which this thing is the main entity being described. See <a href="/docs/datamodel.html#mainEntityBackground">background notes</a> for details.</para>
        /// </summary>
        public IdentifierValue<Entity> MainEntityOfPage_as_URL { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>Indicates a page (or other CreativeWork) for which this thing is the main entity being described. See <a href="/docs/datamodel.html#mainEntityBackground">background notes</a> for details.</para>
        /// </summary>
        public IdentifierValue<Entity> MainEntityOfPage_as_CreativeWork { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>A description of the item.</para>
        /// </summary>
        public TextValue Description { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>A sub property of description. A short description of the item used to disambiguate from other, similar items. Information from other properties (in particular, name) may be necessary for the description to be useful for disambiguation.</para>
        /// </summary>
        public TextValue DisambiguatingDescription { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>An alias for the item.</para>
        /// </summary>
        public TextValue AlternateName { get; private set; }
    }
}
