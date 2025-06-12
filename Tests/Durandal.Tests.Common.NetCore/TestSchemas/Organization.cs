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
    /// <para>Organization</para>
    /// <para>An organization such as a school, NGO, corporation, club, etc.</para>
    /// </summary>
    public class Organization : Entity
    {
        private static HashSet<string> _inheritance = new HashSet<string>() { "http://schema.org/Thing" };

        public Organization(KnowledgeContext context, string entityId = null) : base(context, "http://schema.org/Organization", entityId) { Initialize(); }

        /// <summary>
        /// Casting constructor
        /// </summary>
        /// <param name="castFrom">The entity that this one is being cast from</param>
        public Organization(Entity castFrom) : base(castFrom, "http://schema.org/Organization") { Initialize(); }

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
            ServiceArea_as_AdministrativeArea = new IdentifierValue<AdministrativeArea>(_context, EntityId, "serviceArea");
            Funder_as_Person = new IdentifierValue<Person>(_context, EntityId, "funder");
            Funder_as_Organization = new IdentifierValue<Organization>(_context, EntityId, "funder");
            AreaServed_as_GeoShape = new IdentifierValue<GeoShape>(_context, EntityId, "areaServed");
            AreaServed_as_string = new TextValue(_context, EntityId, "areaServed");
            AreaServed_as_Place = new IdentifierValue<Place>(_context, EntityId, "areaServed");
            AreaServed_as_AdministrativeArea = new IdentifierValue<AdministrativeArea>(_context, EntityId, "areaServed");
            MemberOf_as_Organization = new IdentifierValue<Organization>(_context, EntityId, "memberOf");
            MemberOf_as_ProgramMembership = new IdentifierValue<Entity>(_context, EntityId, "memberOf");
            Events = new IdentifierValue<Entity>(_context, EntityId, "events");
            SubOrganization = new IdentifierValue<Organization>(_context, EntityId, "subOrganization");
            HasOfferCatalog = new IdentifierValue<Entity>(_context, EntityId, "hasOfferCatalog");
            GlobalLocationNumber = new TextValue(_context, EntityId, "globalLocationNumber");
            Reviews = new IdentifierValue<Entity>(_context, EntityId, "reviews");
            Members_as_Person = new IdentifierValue<Person>(_context, EntityId, "members");
            Members_as_Organization = new IdentifierValue<Organization>(_context, EntityId, "members");
            AggregateRating = new IdentifierValue<Entity>(_context, EntityId, "aggregateRating");
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
            Address_as_PostalAddress = new IdentifierValue<PostalAddress>(_context, EntityId, "address");
            Address_as_string = new TextValue(_context, EntityId, "address");
            Logo_as_URL = new IdentifierValue<Entity>(_context, EntityId, "logo");
            Logo_as_ImageObject = new IdentifierValue<Entity>(_context, EntityId, "logo");
            Employees = new IdentifierValue<Person>(_context, EntityId, "employees");
            Telephone = new TextValue(_context, EntityId, "telephone");
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
            Review = new IdentifierValue<Entity>(_context, EntityId, "review");
            FoundingLocation = new IdentifierValue<Place>(_context, EntityId, "foundingLocation");
            Owns_as_Product = new IdentifierValue<Entity>(_context, EntityId, "owns");
            Owns_as_OwnershipInfo = new IdentifierValue<Entity>(_context, EntityId, "owns");
            Event = new IdentifierValue<Entity>(_context, EntityId, "event");
            Founder = new IdentifierValue<Person>(_context, EntityId, "founder");
            PublishingPrinciples_as_CreativeWork = new IdentifierValue<Entity>(_context, EntityId, "publishingPrinciples");
            PublishingPrinciples_as_URL = new IdentifierValue<Entity>(_context, EntityId, "publishingPrinciples");
            Sponsor_as_Person = new IdentifierValue<Person>(_context, EntityId, "sponsor");
            Sponsor_as_Organization = new IdentifierValue<Organization>(_context, EntityId, "sponsor");
            IsicV4 = new TextValue(_context, EntityId, "isicV4");
            Location_as_Place = new IdentifierValue<Place>(_context, EntityId, "location");
            Location_as_PostalAddress = new IdentifierValue<PostalAddress>(_context, EntityId, "location");
            Location_as_string = new TextValue(_context, EntityId, "location");
            Brand_as_Organization = new IdentifierValue<Organization>(_context, EntityId, "brand");
            Brand_as_Brand = new IdentifierValue<Entity>(_context, EntityId, "brand");
            VatID = new TextValue(_context, EntityId, "vatID");
            LeiCode = new TextValue(_context, EntityId, "leiCode");
            FaxNumber = new TextValue(_context, EntityId, "faxNumber");
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
        /// <para>Upcoming or past events associated with this place or organization.</para>
        /// </summary>
        public IdentifierValue<Entity> Events { get; private set; }
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
        /// <para>The <a href="http://www.gs1.org/gln">Global Location Number</a> (GLN, sometimes also referred to as International Location Number or ILN) of the respective organization, person, or place. The GLN is a 13-digit number used to identify parties and physical locations.</para>
        /// </summary>
        public TextValue GlobalLocationNumber { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>Review of the item.</para>
        /// </summary>
        public IdentifierValue<Entity> Reviews { get; private set; }
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
        /// <para>The overall rating, based on a collection of reviews or ratings, of the item.</para>
        /// </summary>
        public IdentifierValue<Entity> AggregateRating { get; private set; }
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
        /// <para>Physical address of the item.</para>
        /// </summary>
        public IdentifierValue<PostalAddress> Address_as_PostalAddress { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>Physical address of the item.</para>
        /// </summary>
        public TextValue Address_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>An associated logo.</para>
        /// </summary>
        public IdentifierValue<Entity> Logo_as_URL { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>An associated logo.</para>
        /// </summary>
        public IdentifierValue<Entity> Logo_as_ImageObject { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>People working for this organization.</para>
        /// </summary>
        public IdentifierValue<Person> Employees { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>The telephone number.</para>
        /// </summary>
        public TextValue Telephone { get; private set; }
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
        /// <para>A review of the item.</para>
        /// </summary>
        public IdentifierValue<Entity> Review { get; private set; }
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
        /// <para>Upcoming or past event associated with this place, organization, or action.</para>
        /// </summary>
        public IdentifierValue<Entity> Event { get; private set; }
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
        /// <para>The International Standard of Industrial Classification of All Economic Activities (ISIC), Revision 4 code for a particular organization, business person, or place.</para>
        /// </summary>
        public TextValue IsicV4 { get; private set; }
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
        /// <summary>
        /// <para>(From http://schema.org/Organization)</para>
        /// <para>The fax number.</para>
        /// </summary>
        public TextValue FaxNumber { get; private set; }
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
