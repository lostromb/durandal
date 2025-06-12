using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Ontology;

namespace Durandal.Plugins.Basic.SchemaDotOrg
{
    /// <summary>
    /// <para>GeoCircle</para>
    /// <para>A GeoCircle is a GeoShape representing a circular geographic area. As it is a GeoShape</para>
    /// <para>          it provides the simple textual property 'circle', but also allows the combination of postalCode alongside geoRadius.</para>
    /// <para>          The center of the circle can be indicated via the 'geoMidpoint' property, or more approximately using 'address', 'postalCode'.</para>
    /// </summary>
    internal class GeoCircle : Entity
    {
        private static HashSet<string> _inheritance = new HashSet<string>() { "http://schema.org/GeoShape", "http://schema.org/StructuredValue", "http://schema.org/Intangible", "http://schema.org/Thing" };

        public GeoCircle(KnowledgeContext context, string entityId = null) : base(context, "http://schema.org/GeoCircle", entityId) { Initialize(); }

        /// <summary>
        /// Casting constructor
        /// </summary>
        /// <param name="castFrom">The entity that this one is being cast from</param>
        public GeoCircle(Entity castFrom) : base(castFrom, "http://schema.org/GeoCircle") { Initialize(); }

        protected override ISet<string> InheritsFromInternal
        {
            get
            {
                return _inheritance;
            }
        }

        private void Initialize()
        {
            GeoRadius_as_Distance = new IdentifierValue<Entity>(_context, EntityId, "geoRadius");
            GeoRadius_as_number = new NumberValue(_context, EntityId, "geoRadius");
            GeoRadius_as_string = new TextValue(_context, EntityId, "geoRadius");
            GeoMidpoint = new IdentifierValue<GeoCoordinates>(_context, EntityId, "geoMidpoint");
            Line = new TextValue(_context, EntityId, "line");
            Address_as_PostalAddress = new IdentifierValue<PostalAddress>(_context, EntityId, "address");
            Address_as_string = new TextValue(_context, EntityId, "address");
            Circle = new TextValue(_context, EntityId, "circle");
            Box = new TextValue(_context, EntityId, "box");
            AddressCountry_as_Country = new IdentifierValue<Entity>(_context, EntityId, "addressCountry");
            AddressCountry_as_string = new TextValue(_context, EntityId, "addressCountry");
            PostalCode = new TextValue(_context, EntityId, "postalCode");
            Elevation_as_string = new TextValue(_context, EntityId, "elevation");
            Elevation_as_number = new NumberValue(_context, EntityId, "elevation");
            Polygon = new TextValue(_context, EntityId, "polygon");
            SameAs = new TextValue(_context, EntityId, "sameAs");
            Url = new TextValue(_context, EntityId, "url");
            Image_as_ImageObject = new IdentifierValue<Entity>(_context, EntityId, "image");
            Image_as_URL = new TextValue(_context, EntityId, "image");
            AdditionalType = new TextValue(_context, EntityId, "additionalType");
            Name = new TextValue(_context, EntityId, "name");
            Identifier_as_PropertyValue = new IdentifierValue<Entity>(_context, EntityId, "identifier");
            Identifier_as_string = new TextValue(_context, EntityId, "identifier");
            Identifier_as_URL = new TextValue(_context, EntityId, "identifier");
            PotentialAction = new IdentifierValue<Entity>(_context, EntityId, "potentialAction");
            MainEntityOfPage_as_URL = new TextValue(_context, EntityId, "mainEntityOfPage");
            MainEntityOfPage_as_CreativeWork = new IdentifierValue<Entity>(_context, EntityId, "mainEntityOfPage");
            Description = new TextValue(_context, EntityId, "description");
            DisambiguatingDescription = new TextValue(_context, EntityId, "disambiguatingDescription");
            AlternateName = new TextValue(_context, EntityId, "alternateName");
        }

        /// <summary>
        /// <para>(From http://schema.org/GeoCircle)</para>
        /// <para>Indicates the approximate radius of a GeoCircle (metres unless indicated otherwise via Distance notation).</para>
        /// </summary>
        public IdentifierValue<Entity> GeoRadius_as_Distance { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/GeoCircle)</para>
        /// <para>Indicates the approximate radius of a GeoCircle (metres unless indicated otherwise via Distance notation).</para>
        /// </summary>
        public NumberValue GeoRadius_as_number { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/GeoCircle)</para>
        /// <para>Indicates the approximate radius of a GeoCircle (metres unless indicated otherwise via Distance notation).</para>
        /// </summary>
        public TextValue GeoRadius_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/GeoCircle)</para>
        /// <para>Indicates the GeoCoordinates at the centre of a GeoShape e.g. GeoCircle.</para>
        /// </summary>
        public IdentifierValue<GeoCoordinates> GeoMidpoint { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/GeoShape)</para>
        /// <para>A line is a point-to-point path consisting of two or more points. A line is expressed as a series of two or more point objects separated by space.</para>
        /// </summary>
        public TextValue Line { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/GeoShape)</para>
        /// <para>Physical address of the item.</para>
        /// </summary>
        public IdentifierValue<PostalAddress> Address_as_PostalAddress { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/GeoShape)</para>
        /// <para>Physical address of the item.</para>
        /// </summary>
        public TextValue Address_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/GeoShape)</para>
        /// <para>A circle is the circular region of a specified radius centered at a specified latitude and longitude. A circle is expressed as a pair followed by a radius in meters.</para>
        /// </summary>
        public TextValue Circle { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/GeoShape)</para>
        /// <para>A box is the area enclosed by the rectangle formed by two points. The first point is the lower corner, the second point is the upper corner. A box is expressed as two points separated by a space character.</para>
        /// </summary>
        public TextValue Box { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/GeoShape)</para>
        /// <para>The country. For example, USA. You can also provide the two-letter <a href="http://en.wikipedia.org/wiki/ISO_3166-1">ISO 3166-1 alpha-2 country code</a>.</para>
        /// </summary>
        public IdentifierValue<Entity> AddressCountry_as_Country { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/GeoShape)</para>
        /// <para>The country. For example, USA. You can also provide the two-letter <a href="http://en.wikipedia.org/wiki/ISO_3166-1">ISO 3166-1 alpha-2 country code</a>.</para>
        /// </summary>
        public TextValue AddressCountry_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/GeoShape)</para>
        /// <para>The postal code. For example, 94043.</para>
        /// </summary>
        public TextValue PostalCode { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/GeoShape)</para>
        /// <para>The elevation of a location (<a href="https://en.wikipedia.org/wiki/World_Geodetic_System">WGS 84</a>).</para>
        /// </summary>
        public TextValue Elevation_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/GeoShape)</para>
        /// <para>The elevation of a location (<a href="https://en.wikipedia.org/wiki/World_Geodetic_System">WGS 84</a>).</para>
        /// </summary>
        public NumberValue Elevation_as_number { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/GeoShape)</para>
        /// <para>A polygon is the area enclosed by a point-to-point path for which the starting and ending points are the same. A polygon is expressed as a series of four or more space delimited points where the first and final points are identical.</para>
        /// </summary>
        public TextValue Polygon { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>URL of a reference Web page that unambiguously indicates the item's identity. E.g. the URL of the item's Wikipedia page, Wikidata entry, or official website.</para>
        /// </summary>
        public TextValue SameAs { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>URL of the item.</para>
        /// </summary>
        public TextValue Url { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>An image of the item. This can be a <a class="localLink" href="http://schema.org/URL">URL</a> or a fully described <a class="localLink" href="http://schema.org/ImageObject">ImageObject</a>.</para>
        /// </summary>
        public IdentifierValue<Entity> Image_as_ImageObject { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>An image of the item. This can be a <a class="localLink" href="http://schema.org/URL">URL</a> or a fully described <a class="localLink" href="http://schema.org/ImageObject">ImageObject</a>.</para>
        /// </summary>
        public TextValue Image_as_URL { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>An additional type for the item, typically used for adding more specific types from external vocabularies in microdata syntax. This is a relationship between something and a class that the thing is in. In RDFa syntax, it is better to use the native RDFa syntax - the 'typeof' attribute - for multiple types. Schema.org tools may have only weaker understanding of extra types, in particular those defined externally.</para>
        /// </summary>
        public TextValue AdditionalType { get; private set; }
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
        public TextValue Identifier_as_URL { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>Indicates a potential Action, which describes an idealized action in which this thing would play an 'object' role.</para>
        /// </summary>
        public IdentifierValue<Entity> PotentialAction { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>Indicates a page (or other CreativeWork) for which this thing is the main entity being described. See <a href="/docs/datamodel.html#mainEntityBackground">background notes</a> for details.</para>
        /// </summary>
        public TextValue MainEntityOfPage_as_URL { get; private set; }
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
