using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Ontology;

namespace Durandal.Plugins.Basic.SchemaDotOrg
{
    /// <summary>
    /// <para>GeoCoordinates</para>
    /// <para>The geographic coordinates of a place or event.</para>
    /// </summary>
    internal class GeoCoordinates : Entity
    {
        private static HashSet<string> _inheritance = new HashSet<string>() { "http://schema.org/StructuredValue", "http://schema.org/Intangible", "http://schema.org/Thing" };

        public GeoCoordinates(KnowledgeContext context, string entityId = null) : base(context, "http://schema.org/GeoCoordinates", entityId) { Initialize(); }

        /// <summary>
        /// Casting constructor
        /// </summary>
        /// <param name="castFrom">The entity that this one is being cast from</param>
        public GeoCoordinates(Entity castFrom) : base(castFrom, "http://schema.org/GeoCoordinates") { Initialize(); }

        protected override ISet<string> InheritsFromInternal
        {
            get
            {
                return _inheritance;
            }
        }

        private void Initialize()
        {
            Latitude_as_number = new NumberValue(_context, EntityId, "latitude");
            Latitude_as_string = new TextValue(_context, EntityId, "latitude");
            Longitude_as_number = new NumberValue(_context, EntityId, "longitude");
            Longitude_as_string = new TextValue(_context, EntityId, "longitude");
            Address_as_PostalAddress = new IdentifierValue<PostalAddress>(_context, EntityId, "address");
            Address_as_string = new TextValue(_context, EntityId, "address");
            AddressCountry_as_Country = new IdentifierValue<Entity>(_context, EntityId, "addressCountry");
            AddressCountry_as_string = new TextValue(_context, EntityId, "addressCountry");
            PostalCode = new TextValue(_context, EntityId, "postalCode");
            Elevation_as_string = new TextValue(_context, EntityId, "elevation");
            Elevation_as_number = new NumberValue(_context, EntityId, "elevation");
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
        /// <para>(From http://schema.org/GeoCoordinates)</para>
        /// <para>The latitude of a location. For example <code>37.42242</code> (<a href="https://en.wikipedia.org/wiki/World_Geodetic_System">WGS 84</a>).</para>
        /// </summary>
        public NumberValue Latitude_as_number { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/GeoCoordinates)</para>
        /// <para>The latitude of a location. For example <code>37.42242</code> (<a href="https://en.wikipedia.org/wiki/World_Geodetic_System">WGS 84</a>).</para>
        /// </summary>
        public TextValue Latitude_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/GeoCoordinates)</para>
        /// <para>The longitude of a location. For example <code>-122.08585</code> (<a href="https://en.wikipedia.org/wiki/World_Geodetic_System">WGS 84</a>).</para>
        /// </summary>
        public NumberValue Longitude_as_number { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/GeoCoordinates)</para>
        /// <para>The longitude of a location. For example <code>-122.08585</code> (<a href="https://en.wikipedia.org/wiki/World_Geodetic_System">WGS 84</a>).</para>
        /// </summary>
        public TextValue Longitude_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/GeoCoordinates)</para>
        /// <para>Physical address of the item.</para>
        /// </summary>
        public IdentifierValue<PostalAddress> Address_as_PostalAddress { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/GeoCoordinates)</para>
        /// <para>Physical address of the item.</para>
        /// </summary>
        public TextValue Address_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/GeoCoordinates)</para>
        /// <para>The country. For example, USA. You can also provide the two-letter <a href="http://en.wikipedia.org/wiki/ISO_3166-1">ISO 3166-1 alpha-2 country code</a>.</para>
        /// </summary>
        public IdentifierValue<Entity> AddressCountry_as_Country { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/GeoCoordinates)</para>
        /// <para>The country. For example, USA. You can also provide the two-letter <a href="http://en.wikipedia.org/wiki/ISO_3166-1">ISO 3166-1 alpha-2 country code</a>.</para>
        /// </summary>
        public TextValue AddressCountry_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/GeoCoordinates)</para>
        /// <para>The postal code. For example, 94043.</para>
        /// </summary>
        public TextValue PostalCode { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/GeoCoordinates)</para>
        /// <para>The elevation of a location (<a href="https://en.wikipedia.org/wiki/World_Geodetic_System">WGS 84</a>).</para>
        /// </summary>
        public TextValue Elevation_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/GeoCoordinates)</para>
        /// <para>The elevation of a location (<a href="https://en.wikipedia.org/wiki/World_Geodetic_System">WGS 84</a>).</para>
        /// </summary>
        public NumberValue Elevation_as_number { get; private set; }
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
