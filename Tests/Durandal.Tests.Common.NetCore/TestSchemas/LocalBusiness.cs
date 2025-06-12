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
    /// <para>LocalBusiness</para>
    /// <para>A particular physical business or branch of an organization. Examples of LocalBusiness include a restaurant, a particular branch of a restaurant chain, a branch of a bank, a medical practice, a club, a bowling alley, etc.</para>
    /// </summary>
    public class LocalBusiness : Entity
    {
        private static HashSet<string> _inheritance = new HashSet<string>() { "http://schema.org/Place", "http://schema.org/Organization", "http://schema.org/Thing" };

        public LocalBusiness(KnowledgeContext context, string entityId = null) : base(context, "http://schema.org/LocalBusiness", entityId) { Initialize(); }

        /// <summary>
        /// Casting constructor
        /// </summary>
        /// <param name="castFrom">The entity that this one is being cast from</param>
        public LocalBusiness(Entity castFrom) : base(castFrom, "http://schema.org/LocalBusiness") { Initialize(); }

        protected override ISet<string> InheritsFromInternal
        {
            get
            {
                return _inheritance;
            }
        }

        private void Initialize()
        {
            PriceRange = new TextValue(_context, EntityId, "priceRange");
            BranchOf = new IdentifierValue<Organization>(_context, EntityId, "branchOf");
            PaymentAccepted = new TextValue(_context, EntityId, "paymentAccepted");
            OpeningHours = new TextValue(_context, EntityId, "openingHours");
            CurrenciesAccepted = new TextValue(_context, EntityId, "currenciesAccepted");
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
            Address_as_PostalAddress = new IdentifierValue<PostalAddress>(_context, EntityId, "address");
            Address_as_string = new TextValue(_context, EntityId, "address");
            SpecialOpeningHoursSpecification = new IdentifierValue<Entity>(_context, EntityId, "specialOpeningHoursSpecification");
            AmenityFeature = new IdentifierValue<Entity>(_context, EntityId, "amenityFeature");
            Logo_as_URL = new IdentifierValue<Entity>(_context, EntityId, "logo");
            Logo_as_ImageObject = new IdentifierValue<Entity>(_context, EntityId, "logo");
            Telephone = new TextValue(_context, EntityId, "telephone");
            Geo_as_GeoCoordinates = new IdentifierValue<GeoCoordinates>(_context, EntityId, "geo");
            Geo_as_GeoShape = new IdentifierValue<GeoShape>(_context, EntityId, "geo");
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
            ServiceArea_as_Place = new IdentifierValue<Place>(_context, EntityId, "serviceArea");
            ServiceArea_as_GeoShape = new IdentifierValue<GeoShape>(_context, EntityId, "serviceArea");
            ServiceArea_as_AdministrativeArea = new IdentifierValue<AdministrativeArea>(_context, EntityId, "serviceArea");
            Funder_as_Person = new IdentifierValue<Person>(_context, EntityId, "funder");
            Funder_as_Organization = new IdentifierValue<Organization>(_context, EntityId, "funder");
            AreaServed_as_GeoShape = new IdentifierValue<GeoShape>(_context, EntityId, "areaServed");
            AreaServed_as_string = new TextValue(_context, EntityId, "areaServed");
            AreaServed_as_Place = new IdentifierValue<Place>(_context, EntityId, "areaServed");
            AreaServed_as_AdministrativeArea = new IdentifierValue<AdministrativeArea>(_context, EntityId, "areaServed");
            MemberOf_as_Organization = new IdentifierValue<Organization>(_context, EntityId, "memberOf");
            MemberOf_as_ProgramMembership = new IdentifierValue<Entity>(_context, EntityId, "memberOf");
            SubOrganization = new IdentifierValue<Organization>(_context, EntityId, "subOrganization");
            HasOfferCatalog = new IdentifierValue<Entity>(_context, EntityId, "hasOfferCatalog");
            Members_as_Person = new IdentifierValue<Person>(_context, EntityId, "members");
            Members_as_Organization = new IdentifierValue<Organization>(_context, EntityId, "members");
            Duns = new TextValue(_context, EntityId, "duns");
            TaxID = new TextValue(_context, EntityId, "taxID");
            Award = new TextValue(_context, EntityId, "award");
            MakesOffer = new IdentifierValue<Entity>(_context, EntityId, "makesOffer");
            ContactPoints = new IdentifierValue<ContactPoint>(_context, EntityId, "contactPoints");
            Awards = new TextValue(_context, EntityId, "awards");
            Seeks = new IdentifierValue<Entity>(_context, EntityId, "seeks");
            Member_as_Organization = new IdentifierValue<Organization>(_context, EntityId, "member");
            Member_as_Person = new IdentifierValue<Person>(_context, EntityId, "member");
            Founders = new IdentifierValue<Person>(_context, EntityId, "founders");
            Alumni = new IdentifierValue<Person>(_context, EntityId, "alumni");
            DissolutionDate = new TimeValue(_context, EntityId, "dissolutionDate");
            Employees = new IdentifierValue<Person>(_context, EntityId, "employees");
            Email = new TextValue(_context, EntityId, "email");
            Department = new IdentifierValue<Organization>(_context, EntityId, "department");
            ContactPoint = new IdentifierValue<ContactPoint>(_context, EntityId, "contactPoint");
            ParentOrganization = new IdentifierValue<Organization>(_context, EntityId, "parentOrganization");
            LegalName = new TextValue(_context, EntityId, "legalName");
            FoundingDate = new TimeValue(_context, EntityId, "foundingDate");
            Employee = new IdentifierValue<Person>(_context, EntityId, "employee");
            NumberOfEmployees = new IdentifierValue<QuantitativeValue>(_context, EntityId, "numberOfEmployees");
            Naics = new TextValue(_context, EntityId, "naics");
            HasPOS = new IdentifierValue<Place>(_context, EntityId, "hasPOS");
            FoundingLocation = new IdentifierValue<Place>(_context, EntityId, "foundingLocation");
            Owns_as_Product = new IdentifierValue<Entity>(_context, EntityId, "owns");
            Owns_as_OwnershipInfo = new IdentifierValue<Entity>(_context, EntityId, "owns");
            Founder = new IdentifierValue<Person>(_context, EntityId, "founder");
            PublishingPrinciples_as_CreativeWork = new IdentifierValue<Entity>(_context, EntityId, "publishingPrinciples");
            PublishingPrinciples_as_URL = new IdentifierValue<Entity>(_context, EntityId, "publishingPrinciples");
            Sponsor_as_Person = new IdentifierValue<Person>(_context, EntityId, "sponsor");
            Sponsor_as_Organization = new IdentifierValue<Organization>(_context, EntityId, "sponsor");
            Location_as_Place = new IdentifierValue<Place>(_context, EntityId, "location");
            Location_as_PostalAddress = new IdentifierValue<PostalAddress>(_context, EntityId, "location");
            Location_as_string = new TextValue(_context, EntityId, "location");
            Brand_as_Organization = new IdentifierValue<Organization>(_context, EntityId, "brand");
            Brand_as_Brand = new IdentifierValue<Entity>(_context, EntityId, "brand");
            VatID = new TextValue(_context, EntityId, "vatID");
            LeiCode = new TextValue(_context, EntityId, "leiCode");
        }

        /// <summary>
        /// <para>(From http://schema.org/LocalBusiness)</para>
        /// <para>The price range of the business, for example <code>$$$</code>.</para>
        /// </summary>
        public TextValue PriceRange { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/LocalBusiness)</para>
        /// <para>The larger organization that this local business is a branch of, if any. Not to be confused with (anatomical)<a class="localLink" href="http://schema.org/branch">branch</a>.</para>
        /// </summary>
        public IdentifierValue<Organization> BranchOf { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/LocalBusiness)</para>
        /// <para>Cash, credit card, etc.</para>
        /// </summary>
        public TextValue PaymentAccepted { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/LocalBusiness)</para>
        /// <para><p>The general opening hours for a business. Opening hours can be specified as a weekly time range, starting with days, then times per day. Multiple days can be listed with commas ',' separating each day. Day or time ranges are specified using a hyphen '-'.</p></para>
        /// <para><ul></para>
        /// <para><li>Days are specified using the following two-letter combinations: <code>Mo</code>, <code>Tu</code>, <code>We</code>, <code>Th</code>, <code>Fr</code>, <code>Sa</code>, <code>Su</code>.</li></para>
        /// <para><li>Times are specified using 24:00 time. For example, 3pm is specified as <code>15:00</code>. </li></para>
        /// <para><li>Here is an example: <code>&lt;time itemprop="openingHours" datetime=&quot;Tu,Th 16:00-20:00&quot;&gt;Tuesdays and Thursdays 4-8pm&lt;/time&gt;</code>.</li></para>
        /// <para><li>If a business is open 7 days a week, then it can be specified as <code>&lt;time itemprop=&quot;openingHours&quot; datetime=&quot;Mo-Su&quot;&gt;Monday through Sunday, all day&lt;/time&gt;</code>.</li></para>
        /// <para></ul></para>
        /// </summary>
        public TextValue OpeningHours { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/LocalBusiness)</para>
        /// <para>The currency accepted (in <a href="http://en.wikipedia.org/wiki/ISO_4217">ISO 4217 currency format</a>).</para>
        /// </summary>
        public TextValue CurrenciesAccepted { get; private set; }
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
        public IdentifierValue<PostalAddress> Address_as_PostalAddress { get; private set; }
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
        public IdentifierValue<GeoShape> Geo_as_GeoShape { get; private set; }
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
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>The geographic area where the service is provided.</para>
        /// </summary>
        public IdentifierValue<Place> ServiceArea_as_Place { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>The geographic area where the service is provided.</para>
        /// </summary>
        public IdentifierValue<GeoShape> ServiceArea_as_GeoShape { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>The geographic area where the service is provided.</para>
        /// </summary>
        public IdentifierValue<AdministrativeArea> ServiceArea_as_AdministrativeArea { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>A person or organization that supports (sponsors) something through some kind of financial contribution.</para>
        /// </summary>
        public IdentifierValue<Person> Funder_as_Person { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>A person or organization that supports (sponsors) something through some kind of financial contribution.</para>
        /// </summary>
        public IdentifierValue<Organization> Funder_as_Organization { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>The geographic area where a service or offered item is provided.</para>
        /// </summary>
        public IdentifierValue<GeoShape> AreaServed_as_GeoShape { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>The geographic area where a service or offered item is provided.</para>
        /// </summary>
        public TextValue AreaServed_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>The geographic area where a service or offered item is provided.</para>
        /// </summary>
        public IdentifierValue<Place> AreaServed_as_Place { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>The geographic area where a service or offered item is provided.</para>
        /// </summary>
        public IdentifierValue<AdministrativeArea> AreaServed_as_AdministrativeArea { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>An Organization (or ProgramMembership) to which this Person or Organization belongs.</para>
        /// </summary>
        public IdentifierValue<Organization> MemberOf_as_Organization { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>An Organization (or ProgramMembership) to which this Person or Organization belongs.</para>
        /// </summary>
        public IdentifierValue<Entity> MemberOf_as_ProgramMembership { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>A relationship between two organizations where the first includes the second, e.g., as a subsidiary. See also: the more specific 'department' property.</para>
        /// </summary>
        public IdentifierValue<Organization> SubOrganization { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>Indicates an OfferCatalog listing for this Organization, Person, or Service.</para>
        /// </summary>
        public IdentifierValue<Entity> HasOfferCatalog { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>A member of this organization.</para>
        /// </summary>
        public IdentifierValue<Person> Members_as_Person { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>A member of this organization.</para>
        /// </summary>
        public IdentifierValue<Organization> Members_as_Organization { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>The Dun &amp; Bradstreet DUNS number for identifying an organization or business person.</para>
        /// </summary>
        public TextValue Duns { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>The Tax / Fiscal ID of the organization or person, e.g. the TIN in the US or the CIF/NIF in Spain.</para>
        /// </summary>
        public TextValue TaxID { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>An award won by or for this item.</para>
        /// </summary>
        public TextValue Award { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>A pointer to products or services offered by the organization or person.</para>
        /// </summary>
        public IdentifierValue<Entity> MakesOffer { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>A contact point for a person or organization.</para>
        /// </summary>
        public IdentifierValue<ContactPoint> ContactPoints { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>Awards won by or for this item.</para>
        /// </summary>
        public TextValue Awards { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>A pointer to products or services sought by the organization or person (demand).</para>
        /// </summary>
        public IdentifierValue<Entity> Seeks { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>A member of an Organization or a ProgramMembership. Organizations can be members of organizations; ProgramMembership is typically for individuals.</para>
        /// </summary>
        public IdentifierValue<Organization> Member_as_Organization { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>A member of an Organization or a ProgramMembership. Organizations can be members of organizations; ProgramMembership is typically for individuals.</para>
        /// </summary>
        public IdentifierValue<Person> Member_as_Person { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>A person who founded this organization.</para>
        /// </summary>
        public IdentifierValue<Person> Founders { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>Alumni of an organization.</para>
        /// </summary>
        public IdentifierValue<Person> Alumni { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>The date that this organization was dissolved.</para>
        /// </summary>
        public TimeValue DissolutionDate { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>People working for this organization.</para>
        /// </summary>
        public IdentifierValue<Person> Employees { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>Email address.</para>
        /// </summary>
        public TextValue Email { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>A relationship between an organization and a department of that organization, also described as an organization (allowing different urls, logos, opening hours). For example: a store with a pharmacy, or a bakery with a cafe.</para>
        /// </summary>
        public IdentifierValue<Organization> Department { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>A contact point for a person or organization.</para>
        /// </summary>
        public IdentifierValue<ContactPoint> ContactPoint { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>The larger organization that this organization is a <a class="localLink" href="http://schema.org/subOrganization">subOrganization</a> of, if any.</para>
        /// </summary>
        public IdentifierValue<Organization> ParentOrganization { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>The official name of the organization, e.g. the registered company name.</para>
        /// </summary>
        public TextValue LegalName { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>The date that this organization was founded.</para>
        /// </summary>
        public TimeValue FoundingDate { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>Someone working for this organization.</para>
        /// </summary>
        public IdentifierValue<Person> Employee { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>The number of employees in an organization e.g. business.</para>
        /// </summary>
        public IdentifierValue<QuantitativeValue> NumberOfEmployees { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>The North American Industry Classification System (NAICS) code for a particular organization or business person.</para>
        /// </summary>
        public TextValue Naics { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>Points-of-Sales operated by the organization or person.</para>
        /// </summary>
        public IdentifierValue<Place> HasPOS { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>The place where the Organization was founded.</para>
        /// </summary>
        public IdentifierValue<Place> FoundingLocation { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>Products owned by the organization or person.</para>
        /// </summary>
        public IdentifierValue<Entity> Owns_as_Product { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>Products owned by the organization or person.</para>
        /// </summary>
        public IdentifierValue<Entity> Owns_as_OwnershipInfo { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>A person who founded this organization.</para>
        /// </summary>
        public IdentifierValue<Person> Founder { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>The publishingPrinciples property indicates (typically via <a class="localLink" href="http://schema.org/URL">URL</a>) a document describing the editorial principles of an <a class="localLink" href="http://schema.org/Organization">Organization</a> (or individual e.g. a <a class="localLink" href="http://schema.org/Person">Person</a> writing a blog) that relate to their activities as a publisher, e.g. ethics or diversity policies. When applied to a <a class="localLink" href="http://schema.org/CreativeWork">CreativeWork</a> (e.g. <a class="localLink" href="http://schema.org/NewsArticle">NewsArticle</a>) the principles are those of the party primarily responsible for the creation of the <a class="localLink" href="http://schema.org/CreativeWork">CreativeWork</a>.</p></para>
        /// <para><p>While such policies are most typically expressed in natural language, sometimes related information (e.g. indicating a <a class="localLink" href="http://schema.org/funder">funder</a>) can be expressed using schema.org terminology.</para>
        /// </summary>
        public IdentifierValue<Entity> PublishingPrinciples_as_CreativeWork { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>The publishingPrinciples property indicates (typically via <a class="localLink" href="http://schema.org/URL">URL</a>) a document describing the editorial principles of an <a class="localLink" href="http://schema.org/Organization">Organization</a> (or individual e.g. a <a class="localLink" href="http://schema.org/Person">Person</a> writing a blog) that relate to their activities as a publisher, e.g. ethics or diversity policies. When applied to a <a class="localLink" href="http://schema.org/CreativeWork">CreativeWork</a> (e.g. <a class="localLink" href="http://schema.org/NewsArticle">NewsArticle</a>) the principles are those of the party primarily responsible for the creation of the <a class="localLink" href="http://schema.org/CreativeWork">CreativeWork</a>.</p></para>
        /// <para><p>While such policies are most typically expressed in natural language, sometimes related information (e.g. indicating a <a class="localLink" href="http://schema.org/funder">funder</a>) can be expressed using schema.org terminology.</para>
        /// </summary>
        public IdentifierValue<Entity> PublishingPrinciples_as_URL { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>A person or organization that supports a thing through a pledge, promise, or financial contribution. e.g. a sponsor of a Medical Study or a corporate sponsor of an event.</para>
        /// </summary>
        public IdentifierValue<Person> Sponsor_as_Person { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>A person or organization that supports a thing through a pledge, promise, or financial contribution. e.g. a sponsor of a Medical Study or a corporate sponsor of an event.</para>
        /// </summary>
        public IdentifierValue<Organization> Sponsor_as_Organization { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>The location of for example where the event is happening, an organization is located, or where an action takes place.</para>
        /// </summary>
        public IdentifierValue<Place> Location_as_Place { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>The location of for example where the event is happening, an organization is located, or where an action takes place.</para>
        /// </summary>
        public IdentifierValue<PostalAddress> Location_as_PostalAddress { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>The location of for example where the event is happening, an organization is located, or where an action takes place.</para>
        /// </summary>
        public TextValue Location_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>The brand(s) associated with a product or service, or the brand(s) maintained by an organization or business person.</para>
        /// </summary>
        public IdentifierValue<Organization> Brand_as_Organization { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>The brand(s) associated with a product or service, or the brand(s) maintained by an organization or business person.</para>
        /// </summary>
        public IdentifierValue<Entity> Brand_as_Brand { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>The Value-added Tax ID of the organization or person.</para>
        /// </summary>
        public TextValue VatID { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>An organization identifier that uniquely identifies a legal entity as defined in ISO 17442.</para>
        /// </summary>
        public TextValue LeiCode { get; private set; }
    }
}
