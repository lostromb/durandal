using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Ontology;

namespace Durandal.Plugins.Weather.SchemaDotOrg
{
    /// <summary>
    /// <para>Place</para>
    /// <para>Entities that have a somewhat fixed, physical extension.</para>
    /// </summary>
    internal class Place : Entity
    {
        private static HashSet<string> _inheritance = new HashSet<string>() { "http://schema.org/Thing" };

        public Place(KnowledgeContext context, string entityId = null) : base(context, "http://schema.org/Place", entityId) { Initialize(); }

        /// <summary>
        /// Casting constructor
        /// </summary>
        /// <param name="castFrom">The entity that this one is being cast from</param>
        public Place(Entity castFrom) : base(castFrom, "http://schema.org/Place") { Initialize(); }

        protected override ISet<string> InheritsFromInternal
        {
            get
            {
                return _inheritance;
            }
        }

        private void Initialize()
        {
            Photo_as_Photograph = new IdentifierValue<Entity>(_context, EntityId, "photo");
            Photo_as_ImageObject = new IdentifierValue<Entity>(_context, EntityId, "photo");
            OpeningHoursSpecification = new IdentifierValue<Entity>(_context, EntityId, "openingHoursSpecification");
            Events = new IdentifierValue<Entity>(_context, EntityId, "events");
            SmokingAllowed = new BooleanValue(_context, EntityId, "smokingAllowed");
            GlobalLocationNumber = new TextValue(_context, EntityId, "globalLocationNumber");
            MaximumAttendeeCapacity = new IdentifierValue<Entity>(_context, EntityId, "maximumAttendeeCapacity");
            Reviews = new IdentifierValue<Entity>(_context, EntityId, "reviews");
            AggregateRating = new IdentifierValue<Entity>(_context, EntityId, "aggregateRating");
            Photos_as_Photograph = new IdentifierValue<Entity>(_context, EntityId, "photos");
            Photos_as_ImageObject = new IdentifierValue<Entity>(_context, EntityId, "photos");
            Map = new IdentifierValue<Entity>(_context, EntityId, "map");
            BranchCode = new TextValue(_context, EntityId, "branchCode");
            HasMap_as_URL = new IdentifierValue<Entity>(_context, EntityId, "hasMap");
            HasMap_as_Map = new IdentifierValue<Entity>(_context, EntityId, "hasMap");
            AdditionalProperty = new IdentifierValue<Entity>(_context, EntityId, "additionalProperty");
            Address_as_PostalAddress = new IdentifierValue<Entity>(_context, EntityId, "address");
            Address_as_string = new TextValue(_context, EntityId, "address");
            SpecialOpeningHoursSpecification = new IdentifierValue<Entity>(_context, EntityId, "specialOpeningHoursSpecification");
            AmenityFeature = new IdentifierValue<Entity>(_context, EntityId, "amenityFeature");
            Logo_as_URL = new IdentifierValue<Entity>(_context, EntityId, "logo");
            Logo_as_ImageObject = new IdentifierValue<Entity>(_context, EntityId, "logo");
            Telephone = new TextValue(_context, EntityId, "telephone");
            Geo_as_GeoCoordinates = new IdentifierValue<GeoCoordinates>(_context, EntityId, "geo");
            Geo_as_GeoShape = new IdentifierValue<Entity>(_context, EntityId, "geo");
            ContainedInPlace = new IdentifierValue<Place>(_context, EntityId, "containedInPlace");
            Review = new IdentifierValue<Entity>(_context, EntityId, "review");
            PublicAccess = new BooleanValue(_context, EntityId, "publicAccess");
            Event = new IdentifierValue<Entity>(_context, EntityId, "event");
            ContainsPlace = new IdentifierValue<Place>(_context, EntityId, "containsPlace");
            IsicV4 = new TextValue(_context, EntityId, "isicV4");
            Maps = new IdentifierValue<Entity>(_context, EntityId, "maps");
            FaxNumber = new TextValue(_context, EntityId, "faxNumber");
            IsAccessibleForFree = new BooleanValue(_context, EntityId, "isAccessibleForFree");
            ContainedIn = new IdentifierValue<Place>(_context, EntityId, "containedIn");
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
        /// <para>(From http://schema.org/Place)</para>
        /// <para>A photograph of this place.</para>
        /// </summary>
        public IdentifierValue<Entity> Photo_as_Photograph { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>A photograph of this place.</para>
        /// </summary>
        public IdentifierValue<Entity> Photo_as_ImageObject { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>The opening hours of a certain place.</para>
        /// </summary>
        public IdentifierValue<Entity> OpeningHoursSpecification { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>Upcoming or past events associated with this place or organization.</para>
        /// </summary>
        public IdentifierValue<Entity> Events { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>Indicates whether it is allowed to smoke in the place, e.g. in the restaurant, hotel or hotel room.</para>
        /// </summary>
        public BooleanValue SmokingAllowed { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>The <a href="http://www.gs1.org/gln">Global Location Number</a> (GLN, sometimes also referred to as International Location Number or ILN) of the respective organization, person, or place. The GLN is a 13-digit number used to identify parties and physical locations.</para>
        /// </summary>
        public TextValue GlobalLocationNumber { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>The total number of individuals that may attend an event or venue.</para>
        /// </summary>
        public IdentifierValue<Entity> MaximumAttendeeCapacity { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>Review of the item.</para>
        /// </summary>
        public IdentifierValue<Entity> Reviews { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>The overall rating, based on a collection of reviews or ratings, of the item.</para>
        /// </summary>
        public IdentifierValue<Entity> AggregateRating { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>Photographs of this place.</para>
        /// </summary>
        public IdentifierValue<Entity> Photos_as_Photograph { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>Photographs of this place.</para>
        /// </summary>
        public IdentifierValue<Entity> Photos_as_ImageObject { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>A URL to a map of the place.</para>
        /// </summary>
        public IdentifierValue<Entity> Map { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>A short textual code (also called "store code") that uniquely identifies a place of business. The code is typically assigned by the parentOrganization and used in structured URLs.</p></para>
        /// <para><p>For example, in the URL http://www.starbucks.co.uk/store-locator/etc/detail/3047 the code "3047" is a branchCode for a particular branch.</para>
        /// </summary>
        public TextValue BranchCode { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>A URL to a map of the place.</para>
        /// </summary>
        public IdentifierValue<Entity> HasMap_as_URL { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>A URL to a map of the place.</para>
        /// </summary>
        public IdentifierValue<Entity> HasMap_as_Map { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>A property-value pair representing an additional characteristics of the entitity, e.g. a product feature or another characteristic for which there is no matching property in schema.org.</p></para>
        /// <para><p>Note: Publishers should be aware that applications designed to use specific schema.org properties (e.g. http://schema.org/width, http://schema.org/color, http://schema.org/gtin13, ...) will typically expect such data to be provided using those properties, rather than using the generic property/value mechanism.</para>
        /// </summary>
        public IdentifierValue<Entity> AdditionalProperty { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>Physical address of the item.</para>
        /// </summary>
        public IdentifierValue<Entity> Address_as_PostalAddress { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>Physical address of the item.</para>
        /// </summary>
        public TextValue Address_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>The special opening hours of a certain place.</p></para>
        /// <para><p>Use this to explicitly override general opening hours brought in scope by <a class="localLink" href="http://schema.org/openingHoursSpecification">openingHoursSpecification</a> or <a class="localLink" href="http://schema.org/openingHours">openingHours</a>.</para>
        /// </summary>
        public IdentifierValue<Entity> SpecialOpeningHoursSpecification { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>An amenity feature (e.g. a characteristic or service) of the Accommodation. This generic property does not make a statement about whether the feature is included in an offer for the main accommodation or available at extra costs.</para>
        /// </summary>
        public IdentifierValue<Entity> AmenityFeature { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>An associated logo.</para>
        /// </summary>
        public IdentifierValue<Entity> Logo_as_URL { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>An associated logo.</para>
        /// </summary>
        public IdentifierValue<Entity> Logo_as_ImageObject { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>The telephone number.</para>
        /// </summary>
        public TextValue Telephone { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>The geo coordinates of the place.</para>
        /// </summary>
        public IdentifierValue<GeoCoordinates> Geo_as_GeoCoordinates { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>The geo coordinates of the place.</para>
        /// </summary>
        public IdentifierValue<Entity> Geo_as_GeoShape { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>The basic containment relation between a place and one that contains it.</para>
        /// </summary>
        public IdentifierValue<Place> ContainedInPlace { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>A review of the item.</para>
        /// </summary>
        public IdentifierValue<Entity> Review { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>A flag to signal that the <a class="localLink" href="http://schema.org/Place">Place</a> is open to public visitors.  If this property is omitted there is no assumed default boolean value</para>
        /// </summary>
        public BooleanValue PublicAccess { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>Upcoming or past event associated with this place, organization, or action.</para>
        /// </summary>
        public IdentifierValue<Entity> Event { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>The basic containment relation between a place and another that it contains.</para>
        /// </summary>
        public IdentifierValue<Place> ContainsPlace { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>The International Standard of Industrial Classification of All Economic Activities (ISIC), Revision 4 code for a particular organization, business person, or place.</para>
        /// </summary>
        public TextValue IsicV4 { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>A URL to a map of the place.</para>
        /// </summary>
        public IdentifierValue<Entity> Maps { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>The fax number.</para>
        /// </summary>
        public TextValue FaxNumber { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>A flag to signal that the item, event, or place is accessible for free.</para>
        /// </summary>
        public BooleanValue IsAccessibleForFree { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Place)</para>
        /// <para>The basic containment relation between a place and one that contains it.</para>
        /// </summary>
        public IdentifierValue<Place> ContainedIn { get; private set; }
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
