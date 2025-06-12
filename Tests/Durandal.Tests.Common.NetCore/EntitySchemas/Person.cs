using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Ontology;
using Durandal.Tests.EntitySchemas;

namespace Durandal.Tests.EntitySchemas
{
    /// <summary>
    /// <para>Person</para>
    /// <para>A person (alive, dead, undead, or fictional).</para>
    /// </summary>
    internal class Person : Entity
    {
        private static HashSet<string> _inheritance = new HashSet<string>() { "http://schema.org/Thing" };

        public Person(KnowledgeContext context, string entityId = null) : base(context, "http://schema.org/Person", entityId) { Initialize(); }

        /// <summary>
        /// Casting constructor
        /// </summary>
        /// <param name="castFrom">The entity that this one is being cast from</param>
        public Person(Entity castFrom) : base(castFrom, "http://schema.org/Person") { Initialize(); }

        protected override ISet<string> InheritsFromInternal
        {
            get
            {
                return _inheritance;
            }
        }

        private void Initialize()
        {
            Spouse = new IdentifierValue<Person>(_context, EntityId, "spouse");
            Funder_as_Person = new IdentifierValue<Person>(_context, EntityId, "funder");
            Funder_as_Organization = new IdentifierValue<Organization>(_context, EntityId, "funder");
            Colleagues = new IdentifierValue<Person>(_context, EntityId, "colleagues");
            DeathDate = new TimeValue(_context, EntityId, "deathDate");
            MemberOf_as_Organization = new IdentifierValue<Organization>(_context, EntityId, "memberOf");
            MemberOf_as_ProgramMembership = new IdentifierValue<Entity>(_context, EntityId, "memberOf");
            Height_as_Distance = new IdentifierValue<Entity>(_context, EntityId, "height");
            Height_as_QuantitativeValue = new IdentifierValue<Entity>(_context, EntityId, "height");
            WorkLocation_as_Place = new IdentifierValue<Place>(_context, EntityId, "workLocation");
            WorkLocation_as_ContactPoint = new IdentifierValue<ContactPoint>(_context, EntityId, "workLocation");
            NetWorth_as_MonetaryAmount = new IdentifierValue<Entity>(_context, EntityId, "netWorth");
            NetWorth_as_PriceSpecification = new IdentifierValue<Entity>(_context, EntityId, "netWorth");
            Children = new IdentifierValue<Person>(_context, EntityId, "children");
            JobTitle = new TextValue(_context, EntityId, "jobTitle");
            HasOfferCatalog = new IdentifierValue<Entity>(_context, EntityId, "hasOfferCatalog");
            DeathPlace = new IdentifierValue<Place>(_context, EntityId, "deathPlace");
            GlobalLocationNumber = new TextValue(_context, EntityId, "globalLocationNumber");
            BirthPlace = new IdentifierValue<Place>(_context, EntityId, "birthPlace");
            Gender_as_string = new TextValue(_context, EntityId, "gender");
            Gender_as_GenderType = new IdentifierValue<GenderType>(_context, EntityId, "gender");
            AlumniOf_as_EducationalOrganization = new IdentifierValue<Entity>(_context, EntityId, "alumniOf");
            AlumniOf_as_Organization = new IdentifierValue<Organization>(_context, EntityId, "alumniOf");
            HomeLocation_as_ContactPoint = new IdentifierValue<ContactPoint>(_context, EntityId, "homeLocation");
            HomeLocation_as_Place = new IdentifierValue<Place>(_context, EntityId, "homeLocation");
            Duns = new TextValue(_context, EntityId, "duns");
            TaxID = new TextValue(_context, EntityId, "taxID");
            Award = new TextValue(_context, EntityId, "award");
            BirthDate = new TimeValue(_context, EntityId, "birthDate");
            MakesOffer = new IdentifierValue<Entity>(_context, EntityId, "makesOffer");
            GivenName = new TextValue(_context, EntityId, "givenName");
            ContactPoints = new IdentifierValue<ContactPoint>(_context, EntityId, "contactPoints");
            Awards = new TextValue(_context, EntityId, "awards");
            FamilyName = new TextValue(_context, EntityId, "familyName");
            Seeks = new IdentifierValue<Entity>(_context, EntityId, "seeks");
            Sibling = new IdentifierValue<Person>(_context, EntityId, "sibling");
            Address_as_PostalAddress = new IdentifierValue<PostalAddress>(_context, EntityId, "address");
            Address_as_string = new TextValue(_context, EntityId, "address");
            PerformerIn = new IdentifierValue<Entity>(_context, EntityId, "performerIn");
            HonorificPrefix = new TextValue(_context, EntityId, "honorificPrefix");
            AdditionalName = new TextValue(_context, EntityId, "additionalName");
            Siblings = new IdentifierValue<Person>(_context, EntityId, "siblings");
            Telephone = new TextValue(_context, EntityId, "telephone");
            Email = new TextValue(_context, EntityId, "email");
            Weight = new IdentifierValue<Entity>(_context, EntityId, "weight");
            ContactPoint = new IdentifierValue<ContactPoint>(_context, EntityId, "contactPoint");
            Colleague_as_URL = new TextValue(_context, EntityId, "colleague");
            Colleague_as_Person = new IdentifierValue<Person>(_context, EntityId, "colleague");
            Naics = new TextValue(_context, EntityId, "naics");
            HasPOS = new IdentifierValue<Place>(_context, EntityId, "hasPOS");
            Parent = new IdentifierValue<Person>(_context, EntityId, "parent");
            Owns_as_Product = new IdentifierValue<Entity>(_context, EntityId, "owns");
            Owns_as_OwnershipInfo = new IdentifierValue<Entity>(_context, EntityId, "owns");
            Affiliation = new IdentifierValue<Organization>(_context, EntityId, "affiliation");
            PublishingPrinciples_as_CreativeWork = new IdentifierValue<CreativeWork>(_context, EntityId, "publishingPrinciples");
            PublishingPrinciples_as_URL = new TextValue(_context, EntityId, "publishingPrinciples");
            Sponsor_as_Person = new IdentifierValue<Person>(_context, EntityId, "sponsor");
            Sponsor_as_Organization = new IdentifierValue<Organization>(_context, EntityId, "sponsor");
            IsicV4 = new TextValue(_context, EntityId, "isicV4");
            Brand_as_Organization = new IdentifierValue<Organization>(_context, EntityId, "brand");
            Brand_as_Brand = new IdentifierValue<Entity>(_context, EntityId, "brand");
            HonorificSuffix = new TextValue(_context, EntityId, "honorificSuffix");
            VatID = new TextValue(_context, EntityId, "vatID");
            Nationality = new IdentifierValue<Entity>(_context, EntityId, "nationality");
            FaxNumber = new TextValue(_context, EntityId, "faxNumber");
            RelatedTo = new IdentifierValue<Person>(_context, EntityId, "relatedTo");
            Follows = new IdentifierValue<Person>(_context, EntityId, "follows");
            Knows = new IdentifierValue<Person>(_context, EntityId, "knows");
            WorksFor = new IdentifierValue<Organization>(_context, EntityId, "worksFor");
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
            MainEntityOfPage_as_CreativeWork = new IdentifierValue<CreativeWork>(_context, EntityId, "mainEntityOfPage");
            Description = new TextValue(_context, EntityId, "description");
            DisambiguatingDescription = new TextValue(_context, EntityId, "disambiguatingDescription");
            AlternateName = new TextValue(_context, EntityId, "alternateName");
        }

        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>The person's spouse.</para>
        /// </summary>
        public IdentifierValue<Person> Spouse { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>A person or organization that supports (sponsors) something through some kind of financial contribution.</para>
        /// </summary>
        public IdentifierValue<Person> Funder_as_Person { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>A person or organization that supports (sponsors) something through some kind of financial contribution.</para>
        /// </summary>
        public IdentifierValue<Organization> Funder_as_Organization { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>A colleague of the person.</para>
        /// </summary>
        public IdentifierValue<Person> Colleagues { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>Date of death.</para>
        /// </summary>
        public TimeValue DeathDate { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>An Organization (or ProgramMembership) to which this Person or Organization belongs.</para>
        /// </summary>
        public IdentifierValue<Organization> MemberOf_as_Organization { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>An Organization (or ProgramMembership) to which this Person or Organization belongs.</para>
        /// </summary>
        public IdentifierValue<Entity> MemberOf_as_ProgramMembership { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>The height of the item.</para>
        /// </summary>
        public IdentifierValue<Entity> Height_as_Distance { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>The height of the item.</para>
        /// </summary>
        public IdentifierValue<Entity> Height_as_QuantitativeValue { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>A contact location for a person's place of work.</para>
        /// </summary>
        public IdentifierValue<Place> WorkLocation_as_Place { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>A contact location for a person's place of work.</para>
        /// </summary>
        public IdentifierValue<ContactPoint> WorkLocation_as_ContactPoint { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>The total financial value of the person as calculated by subtracting assets from liabilities.</para>
        /// </summary>
        public IdentifierValue<Entity> NetWorth_as_MonetaryAmount { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>The total financial value of the person as calculated by subtracting assets from liabilities.</para>
        /// </summary>
        public IdentifierValue<Entity> NetWorth_as_PriceSpecification { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>A child of the person.</para>
        /// </summary>
        public IdentifierValue<Person> Children { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>The job title of the person (for example, Financial Manager).</para>
        /// </summary>
        public TextValue JobTitle { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>Indicates an OfferCatalog listing for this Organization, Person, or Service.</para>
        /// </summary>
        public IdentifierValue<Entity> HasOfferCatalog { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>The place where the person died.</para>
        /// </summary>
        public IdentifierValue<Place> DeathPlace { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>The <a href="http://www.gs1.org/gln">Global Location Number</a> (GLN, sometimes also referred to as International Location Number or ILN) of the respective organization, person, or place. The GLN is a 13-digit number used to identify parties and physical locations.</para>
        /// </summary>
        public TextValue GlobalLocationNumber { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>The place where the person was born.</para>
        /// </summary>
        public IdentifierValue<Place> BirthPlace { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>Gender of the person. While http://schema.org/Male and http://schema.org/Female may be used, text strings are also acceptable for people who do not identify as a binary gender.</para>
        /// </summary>
        public TextValue Gender_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>Gender of the person. While http://schema.org/Male and http://schema.org/Female may be used, text strings are also acceptable for people who do not identify as a binary gender.</para>
        /// </summary>
        public IdentifierValue<GenderType> Gender_as_GenderType { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>An organization that the person is an alumni of.</para>
        /// </summary>
        public IdentifierValue<Entity> AlumniOf_as_EducationalOrganization { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>An organization that the person is an alumni of.</para>
        /// </summary>
        public IdentifierValue<Organization> AlumniOf_as_Organization { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>A contact location for a person's residence.</para>
        /// </summary>
        public IdentifierValue<ContactPoint> HomeLocation_as_ContactPoint { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>A contact location for a person's residence.</para>
        /// </summary>
        public IdentifierValue<Place> HomeLocation_as_Place { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>The Dun &amp; Bradstreet DUNS number for identifying an organization or business person.</para>
        /// </summary>
        public TextValue Duns { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>The Tax / Fiscal ID of the organization or person, e.g. the TIN in the US or the CIF/NIF in Spain.</para>
        /// </summary>
        public TextValue TaxID { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>An award won by or for this item.</para>
        /// </summary>
        public TextValue Award { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>Date of birth.</para>
        /// </summary>
        public TimeValue BirthDate { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>A pointer to products or services offered by the organization or person.</para>
        /// </summary>
        public IdentifierValue<Entity> MakesOffer { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>Given name. In the U.S., the first name of a Person. This can be used along with familyName instead of the name property.</para>
        /// </summary>
        public TextValue GivenName { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>A contact point for a person or organization.</para>
        /// </summary>
        public IdentifierValue<ContactPoint> ContactPoints { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>Awards won by or for this item.</para>
        /// </summary>
        public TextValue Awards { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>Family name. In the U.S., the last name of an Person. This can be used along with givenName instead of the name property.</para>
        /// </summary>
        public TextValue FamilyName { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>A pointer to products or services sought by the organization or person (demand).</para>
        /// </summary>
        public IdentifierValue<Entity> Seeks { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>A sibling of the person.</para>
        /// </summary>
        public IdentifierValue<Person> Sibling { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>Physical address of the item.</para>
        /// </summary>
        public IdentifierValue<PostalAddress> Address_as_PostalAddress { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>Physical address of the item.</para>
        /// </summary>
        public TextValue Address_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>Event that this person is a performer or participant in.</para>
        /// </summary>
        public IdentifierValue<Entity> PerformerIn { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>An honorific prefix preceding a Person's name such as Dr/Mrs/Mr.</para>
        /// </summary>
        public TextValue HonorificPrefix { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>An additional name for a Person, can be used for a middle name.</para>
        /// </summary>
        public TextValue AdditionalName { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>A sibling of the person.</para>
        /// </summary>
        public IdentifierValue<Person> Siblings { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>The telephone number.</para>
        /// </summary>
        public TextValue Telephone { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>Email address.</para>
        /// </summary>
        public TextValue Email { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>The weight of the product or person.</para>
        /// </summary>
        public IdentifierValue<Entity> Weight { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>A contact point for a person or organization.</para>
        /// </summary>
        public IdentifierValue<ContactPoint> ContactPoint { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>A colleague of the person.</para>
        /// </summary>
        public TextValue Colleague_as_URL { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>A colleague of the person.</para>
        /// </summary>
        public IdentifierValue<Person> Colleague_as_Person { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>The North American Industry Classification System (NAICS) code for a particular organization or business person.</para>
        /// </summary>
        public TextValue Naics { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>Points-of-Sales operated by the organization or person.</para>
        /// </summary>
        public IdentifierValue<Place> HasPOS { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>A parent of this person.</para>
        /// </summary>
        public IdentifierValue<Person> Parent { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>Products owned by the organization or person.</para>
        /// </summary>
        public IdentifierValue<Entity> Owns_as_Product { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>Products owned by the organization or person.</para>
        /// </summary>
        public IdentifierValue<Entity> Owns_as_OwnershipInfo { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>An organization that this person is affiliated with. For example, a school/university, a club, or a team.</para>
        /// </summary>
        public IdentifierValue<Organization> Affiliation { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>The publishingPrinciples property indicates (typically via <a class="localLink" href="http://schema.org/URL">URL</a>) a document describing the editorial principles of an <a class="localLink" href="http://schema.org/Organization">Organization</a> (or individual e.g. a <a class="localLink" href="http://schema.org/Person">Person</a> writing a blog) that relate to their activities as a publisher, e.g. ethics or diversity policies. When applied to a <a class="localLink" href="http://schema.org/CreativeWork">CreativeWork</a> (e.g. <a class="localLink" href="http://schema.org/NewsArticle">NewsArticle</a>) the principles are those of the party primarily responsible for the creation of the <a class="localLink" href="http://schema.org/CreativeWork">CreativeWork</a>.</p></para>
        /// <para><p>While such policies are most typically expressed in natural language, sometimes related information (e.g. indicating a <a class="localLink" href="http://schema.org/funder">funder</a>) can be expressed using schema.org terminology.</para>
        /// </summary>
        public IdentifierValue<CreativeWork> PublishingPrinciples_as_CreativeWork { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>The publishingPrinciples property indicates (typically via <a class="localLink" href="http://schema.org/URL">URL</a>) a document describing the editorial principles of an <a class="localLink" href="http://schema.org/Organization">Organization</a> (or individual e.g. a <a class="localLink" href="http://schema.org/Person">Person</a> writing a blog) that relate to their activities as a publisher, e.g. ethics or diversity policies. When applied to a <a class="localLink" href="http://schema.org/CreativeWork">CreativeWork</a> (e.g. <a class="localLink" href="http://schema.org/NewsArticle">NewsArticle</a>) the principles are those of the party primarily responsible for the creation of the <a class="localLink" href="http://schema.org/CreativeWork">CreativeWork</a>.</p></para>
        /// <para><p>While such policies are most typically expressed in natural language, sometimes related information (e.g. indicating a <a class="localLink" href="http://schema.org/funder">funder</a>) can be expressed using schema.org terminology.</para>
        /// </summary>
        public TextValue PublishingPrinciples_as_URL { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>A person or organization that supports a thing through a pledge, promise, or financial contribution. e.g. a sponsor of a Medical Study or a corporate sponsor of an event.</para>
        /// </summary>
        public IdentifierValue<Person> Sponsor_as_Person { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>A person or organization that supports a thing through a pledge, promise, or financial contribution. e.g. a sponsor of a Medical Study or a corporate sponsor of an event.</para>
        /// </summary>
        public IdentifierValue<Organization> Sponsor_as_Organization { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>The International Standard of Industrial Classification of All Economic Activities (ISIC), Revision 4 code for a particular organization, business person, or place.</para>
        /// </summary>
        public TextValue IsicV4 { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>The brand(s) associated with a product or service, or the brand(s) maintained by an organization or business person.</para>
        /// </summary>
        public IdentifierValue<Organization> Brand_as_Organization { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>The brand(s) associated with a product or service, or the brand(s) maintained by an organization or business person.</para>
        /// </summary>
        public IdentifierValue<Entity> Brand_as_Brand { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>An honorific suffix preceding a Person's name such as M.D. /PhD/MSCSW.</para>
        /// </summary>
        public TextValue HonorificSuffix { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>The Value-added Tax ID of the organization or person.</para>
        /// </summary>
        public TextValue VatID { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>Nationality of the person.</para>
        /// </summary>
        public IdentifierValue<Entity> Nationality { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>The fax number.</para>
        /// </summary>
        public TextValue FaxNumber { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>The most generic familial relation.</para>
        /// </summary>
        public IdentifierValue<Person> RelatedTo { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>The most generic uni-directional social relation.</para>
        /// </summary>
        public IdentifierValue<Person> Follows { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>The most generic bi-directional social/work relation.</para>
        /// </summary>
        public IdentifierValue<Person> Knows { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Person)</para>
        /// <para>Organizations that the person works for.</para>
        /// </summary>
        public IdentifierValue<Organization> WorksFor { get; private set; }
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
        public IdentifierValue<CreativeWork> MainEntityOfPage_as_CreativeWork { get; private set; }
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
