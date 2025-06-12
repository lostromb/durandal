using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Ontology;

namespace Durandal.Plugins.Basic.SchemaDotOrg
{
    /// <summary>
    /// <para>ContactPoint</para>
    /// <para>A contact point&#x2014;for example, a Customer Complaints department.</para>
    /// </summary>
    internal class ContactPoint : Entity
    {
        private static HashSet<string> _inheritance = new HashSet<string>() { "http://schema.org/StructuredValue", "http://schema.org/Intangible", "http://schema.org/Thing" };

        public ContactPoint(KnowledgeContext context, string entityId = null) : base(context, "http://schema.org/ContactPoint", entityId) { Initialize(); }

        /// <summary>
        /// Casting constructor
        /// </summary>
        /// <param name="castFrom">The entity that this one is being cast from</param>
        public ContactPoint(Entity castFrom) : base(castFrom, "http://schema.org/ContactPoint") { Initialize(); }

        protected override ISet<string> InheritsFromInternal
        {
            get
            {
                return _inheritance;
            }
        }

        private void Initialize()
        {
            ServiceArea_as_Place = new IdentifierValue<Place>(_context, EntityId, "serviceArea");
            ServiceArea_as_GeoShape = new IdentifierValue<GeoShape>(_context, EntityId, "serviceArea");
            ServiceArea_as_AdministrativeArea = new IdentifierValue<Entity>(_context, EntityId, "serviceArea");
            AreaServed_as_GeoShape = new IdentifierValue<GeoShape>(_context, EntityId, "areaServed");
            AreaServed_as_string = new TextValue(_context, EntityId, "areaServed");
            AreaServed_as_Place = new IdentifierValue<Place>(_context, EntityId, "areaServed");
            AreaServed_as_AdministrativeArea = new IdentifierValue<Entity>(_context, EntityId, "areaServed");
            HoursAvailable = new IdentifierValue<Entity>(_context, EntityId, "hoursAvailable");
            ContactOption = new IdentifierValue<Entity>(_context, EntityId, "contactOption");
            AvailableLanguage_as_string = new TextValue(_context, EntityId, "availableLanguage");
            AvailableLanguage_as_Language = new IdentifierValue<Entity>(_context, EntityId, "availableLanguage");
            Telephone = new TextValue(_context, EntityId, "telephone");
            Email = new TextValue(_context, EntityId, "email");
            ContactType = new TextValue(_context, EntityId, "contactType");
            ProductSupported_as_Product = new IdentifierValue<Entity>(_context, EntityId, "productSupported");
            ProductSupported_as_string = new TextValue(_context, EntityId, "productSupported");
            FaxNumber = new TextValue(_context, EntityId, "faxNumber");
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
        /// <para>(From http://schema.org/ContactPoint)</para>
        /// <para>The geographic area where the service is provided.</para>
        /// </summary>
        public IdentifierValue<Place> ServiceArea_as_Place { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/ContactPoint)</para>
        /// <para>The geographic area where the service is provided.</para>
        /// </summary>
        public IdentifierValue<GeoShape> ServiceArea_as_GeoShape { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/ContactPoint)</para>
        /// <para>The geographic area where the service is provided.</para>
        /// </summary>
        public IdentifierValue<Entity> ServiceArea_as_AdministrativeArea { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/ContactPoint)</para>
        /// <para>The geographic area where a service or offered item is provided.</para>
        /// </summary>
        public IdentifierValue<GeoShape> AreaServed_as_GeoShape { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/ContactPoint)</para>
        /// <para>The geographic area where a service or offered item is provided.</para>
        /// </summary>
        public TextValue AreaServed_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/ContactPoint)</para>
        /// <para>The geographic area where a service or offered item is provided.</para>
        /// </summary>
        public IdentifierValue<Place> AreaServed_as_Place { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/ContactPoint)</para>
        /// <para>The geographic area where a service or offered item is provided.</para>
        /// </summary>
        public IdentifierValue<Entity> AreaServed_as_AdministrativeArea { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/ContactPoint)</para>
        /// <para>The hours during which this service or contact is available.</para>
        /// </summary>
        public IdentifierValue<Entity> HoursAvailable { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/ContactPoint)</para>
        /// <para>An option available on this contact point (e.g. a toll-free number or support for hearing-impaired callers).</para>
        /// </summary>
        public IdentifierValue<Entity> ContactOption { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/ContactPoint)</para>
        /// <para>A language someone may use with or at the item, service or place. Please use one of the language codes from the <a href="http://tools.ietf.org/html/bcp47">IETF BCP 47 standard</a>. See also <a class="localLink" href="http://schema.org/inLanguage">inLanguage</a></para>
        /// </summary>
        public TextValue AvailableLanguage_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/ContactPoint)</para>
        /// <para>A language someone may use with or at the item, service or place. Please use one of the language codes from the <a href="http://tools.ietf.org/html/bcp47">IETF BCP 47 standard</a>. See also <a class="localLink" href="http://schema.org/inLanguage">inLanguage</a></para>
        /// </summary>
        public IdentifierValue<Entity> AvailableLanguage_as_Language { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/ContactPoint)</para>
        /// <para>The telephone number.</para>
        /// </summary>
        public TextValue Telephone { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/ContactPoint)</para>
        /// <para>Email address.</para>
        /// </summary>
        public TextValue Email { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/ContactPoint)</para>
        /// <para>A person or organization can have different contact points, for different purposes. For example, a sales contact point, a PR contact point and so on. This property is used to specify the kind of contact point.</para>
        /// </summary>
        public TextValue ContactType { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/ContactPoint)</para>
        /// <para>The product or service this support contact point is related to (such as product support for a particular product line). This can be a specific product or product line (e.g. "iPhone") or a general category of products or services (e.g. "smartphones").</para>
        /// </summary>
        public IdentifierValue<Entity> ProductSupported_as_Product { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/ContactPoint)</para>
        /// <para>The product or service this support contact point is related to (such as product support for a particular product line). This can be a specific product or product line (e.g. "iPhone") or a general category of products or services (e.g. "smartphones").</para>
        /// </summary>
        public TextValue ProductSupported_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/ContactPoint)</para>
        /// <para>The fax number.</para>
        /// </summary>
        public TextValue FaxNumber { get; private set; }
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
