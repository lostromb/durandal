using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Ontology;

namespace Durandal.Internal.CoreOntology.SchemaDotOrg
{
    /// <summary>
    /// <para>Movie</para>
    /// <para>A movie.</para>
    /// </summary>
    internal class Movie : Entity
    {
        private static HashSet<string> _inheritance = new HashSet<string>() { "http://schema.org/CreativeWork", "http://schema.org/Thing" };

        public Movie(KnowledgeContext context, string entityId = null) : base(context, "http://schema.org/Movie", entityId) { Initialize(); }

        /// <summary>
        /// Casting constructor
        /// </summary>
        /// <param name="castFrom">The entity that this one is being cast from</param>
        public Movie(Entity castFrom) : base(castFrom, "http://schema.org/Movie") { Initialize(); }

        protected override ISet<string> InheritsFromInternal
        {
            get
            {
                return _inheritance;
            }
        }

        private void Initialize()
        {
            Actor = new IdentifierValue<Person>(_context, EntityId, "actor");
            Trailer = new IdentifierValue<Entity>(_context, EntityId, "trailer");
            SubtitleLanguage_as_Language = new IdentifierValue<Entity>(_context, EntityId, "subtitleLanguage");
            SubtitleLanguage_as_string = new TextValue(_context, EntityId, "subtitleLanguage");
            CountryOfOrigin = new IdentifierValue<Entity>(_context, EntityId, "countryOfOrigin");
            MusicBy_as_MusicGroup = new IdentifierValue<Entity>(_context, EntityId, "musicBy");
            MusicBy_as_Person = new IdentifierValue<Person>(_context, EntityId, "musicBy");
            Directors = new IdentifierValue<Person>(_context, EntityId, "directors");
            Director = new IdentifierValue<Person>(_context, EntityId, "director");
            ProductionCompany = new IdentifierValue<Organization>(_context, EntityId, "productionCompany");
            Duration = new IdentifierValue<Entity>(_context, EntityId, "duration");
            Actors = new IdentifierValue<Person>(_context, EntityId, "actors");
            About = new IdentifierValue<Thing>(_context, EntityId, "about");
            EducationalAlignment = new IdentifierValue<Entity>(_context, EntityId, "educationalAlignment");
            AssociatedMedia = new IdentifierValue<Entity>(_context, EntityId, "associatedMedia");
            Funder_as_Person = new IdentifierValue<Person>(_context, EntityId, "funder");
            Funder_as_Organization = new IdentifierValue<Organization>(_context, EntityId, "funder");
            Position_as_Integer = new IdentifierValue<Entity>(_context, EntityId, "position");
            Position_as_string = new TextValue(_context, EntityId, "position");
            Audio = new IdentifierValue<Entity>(_context, EntityId, "audio");
            WorkExample = new IdentifierValue<CreativeWork>(_context, EntityId, "workExample");
            Provider_as_Organization = new IdentifierValue<Organization>(_context, EntityId, "provider");
            Provider_as_Person = new IdentifierValue<Person>(_context, EntityId, "provider");
            Encoding = new IdentifierValue<Entity>(_context, EntityId, "encoding");
            InteractivityType = new TextValue(_context, EntityId, "interactivityType");
            AccessibilitySummary = new TextValue(_context, EntityId, "accessibilitySummary");
            Character = new IdentifierValue<Person>(_context, EntityId, "character");
            Audience = new IdentifierValue<Entity>(_context, EntityId, "audience");
            SourceOrganization = new IdentifierValue<Organization>(_context, EntityId, "sourceOrganization");
            IsPartOf = new IdentifierValue<CreativeWork>(_context, EntityId, "isPartOf");
            Video = new IdentifierValue<Entity>(_context, EntityId, "video");
            Publisher_as_Organization = new IdentifierValue<Organization>(_context, EntityId, "publisher");
            Publisher_as_Person = new IdentifierValue<Person>(_context, EntityId, "publisher");
            Publication = new IdentifierValue<Entity>(_context, EntityId, "publication");
            Text = new TextValue(_context, EntityId, "text");
            Expires = new TimeValue(_context, EntityId, "expires");
            Contributor_as_Person = new IdentifierValue<Person>(_context, EntityId, "contributor");
            Contributor_as_Organization = new IdentifierValue<Organization>(_context, EntityId, "contributor");
            Reviews = new IdentifierValue<Entity>(_context, EntityId, "reviews");
            TypicalAgeRange = new TextValue(_context, EntityId, "typicalAgeRange");
            ReleasedEvent = new IdentifierValue<Entity>(_context, EntityId, "releasedEvent");
            EducationalUse = new TextValue(_context, EntityId, "educationalUse");
            ContentLocation = new IdentifierValue<Place>(_context, EntityId, "contentLocation");
            SchemaVersion_as_URL = new TextValue(_context, EntityId, "schemaVersion");
            SchemaVersion_as_string = new TextValue(_context, EntityId, "schemaVersion");
            AccessibilityFeature = new TextValue(_context, EntityId, "accessibilityFeature");
            AggregateRating = new IdentifierValue<Entity>(_context, EntityId, "aggregateRating");
            AlternativeHeadline = new TextValue(_context, EntityId, "alternativeHeadline");
            LocationCreated = new IdentifierValue<Place>(_context, EntityId, "locationCreated");
            AccessModeSufficient = new TextValue(_context, EntityId, "accessModeSufficient");
            TemporalCoverage_as_time = new TimeValue(_context, EntityId, "temporalCoverage");
            TemporalCoverage_as_URL = new TextValue(_context, EntityId, "temporalCoverage");
            TemporalCoverage_as_string = new TextValue(_context, EntityId, "temporalCoverage");
            AccountablePerson = new IdentifierValue<Person>(_context, EntityId, "accountablePerson");
            SpatialCoverage = new IdentifierValue<Place>(_context, EntityId, "spatialCoverage");
            Offers = new IdentifierValue<Entity>(_context, EntityId, "offers");
            Editor = new IdentifierValue<Person>(_context, EntityId, "editor");
            DiscussionUrl = new TextValue(_context, EntityId, "discussionUrl");
            Award = new TextValue(_context, EntityId, "award");
            CopyrightHolder_as_Organization = new IdentifierValue<Organization>(_context, EntityId, "copyrightHolder");
            CopyrightHolder_as_Person = new IdentifierValue<Person>(_context, EntityId, "copyrightHolder");
            AccessibilityHazard = new TextValue(_context, EntityId, "accessibilityHazard");
            CopyrightYear = new NumberValue(_context, EntityId, "copyrightYear");
            Awards = new TextValue(_context, EntityId, "awards");
            RecordedAt = new IdentifierValue<Entity>(_context, EntityId, "recordedAt");
            CommentCount = new IdentifierValue<Entity>(_context, EntityId, "commentCount");
            FileFormat_as_URL = new TextValue(_context, EntityId, "fileFormat");
            FileFormat_as_string = new TextValue(_context, EntityId, "fileFormat");
            InLanguage_as_Language = new IdentifierValue<Entity>(_context, EntityId, "inLanguage");
            InLanguage_as_string = new TextValue(_context, EntityId, "inLanguage");
            AccessibilityAPI = new TextValue(_context, EntityId, "accessibilityAPI");
            InteractionStatistic = new IdentifierValue<Entity>(_context, EntityId, "interactionStatistic");
            ContentRating = new TextValue(_context, EntityId, "contentRating");
            LearningResourceType = new TextValue(_context, EntityId, "learningResourceType");
            AccessMode = new TextValue(_context, EntityId, "accessMode");
            Material_as_string = new TextValue(_context, EntityId, "material");
            Material_as_URL = new TextValue(_context, EntityId, "material");
            Material_as_Product = new IdentifierValue<Entity>(_context, EntityId, "material");
            IsFamilyFriendly = new BooleanValue(_context, EntityId, "isFamilyFriendly");
            ExampleOfWork = new IdentifierValue<CreativeWork>(_context, EntityId, "exampleOfWork");
            Version_as_string = new TextValue(_context, EntityId, "version");
            Version_as_number = new NumberValue(_context, EntityId, "version");
            DateModified = new TimeValue(_context, EntityId, "dateModified");
            MainEntity = new IdentifierValue<Thing>(_context, EntityId, "mainEntity");
            Genre_as_URL = new TextValue(_context, EntityId, "genre");
            Genre_as_string = new TextValue(_context, EntityId, "genre");
            Keywords = new TextValue(_context, EntityId, "keywords");
            Author_as_Organization = new IdentifierValue<Organization>(_context, EntityId, "author");
            Author_as_Person = new IdentifierValue<Person>(_context, EntityId, "author");
            IsBasedOnUrl_as_Product = new IdentifierValue<Entity>(_context, EntityId, "isBasedOnUrl");
            IsBasedOnUrl_as_CreativeWork = new IdentifierValue<CreativeWork>(_context, EntityId, "isBasedOnUrl");
            IsBasedOnUrl_as_URL = new TextValue(_context, EntityId, "isBasedOnUrl");
            TimeRequired = new IdentifierValue<Entity>(_context, EntityId, "timeRequired");
            Translator_as_Organization = new IdentifierValue<Organization>(_context, EntityId, "translator");
            Translator_as_Person = new IdentifierValue<Person>(_context, EntityId, "translator");
            ThumbnailUrl = new TextValue(_context, EntityId, "thumbnailUrl");
            HasPart = new IdentifierValue<CreativeWork>(_context, EntityId, "hasPart");
            Comment = new IdentifierValue<Entity>(_context, EntityId, "comment");
            Review = new IdentifierValue<Entity>(_context, EntityId, "review");
            License_as_CreativeWork = new IdentifierValue<CreativeWork>(_context, EntityId, "license");
            License_as_URL = new TextValue(_context, EntityId, "license");
            AccessibilityControl = new TextValue(_context, EntityId, "accessibilityControl");
            Encodings = new IdentifierValue<Entity>(_context, EntityId, "encodings");
            IsBasedOn_as_Product = new IdentifierValue<Entity>(_context, EntityId, "isBasedOn");
            IsBasedOn_as_CreativeWork = new IdentifierValue<CreativeWork>(_context, EntityId, "isBasedOn");
            IsBasedOn_as_URL = new TextValue(_context, EntityId, "isBasedOn");
            Creator_as_Organization = new IdentifierValue<Organization>(_context, EntityId, "creator");
            Creator_as_Person = new IdentifierValue<Person>(_context, EntityId, "creator");
            PublishingPrinciples_as_CreativeWork = new IdentifierValue<CreativeWork>(_context, EntityId, "publishingPrinciples");
            PublishingPrinciples_as_URL = new TextValue(_context, EntityId, "publishingPrinciples");
            Sponsor_as_Person = new IdentifierValue<Person>(_context, EntityId, "sponsor");
            Sponsor_as_Organization = new IdentifierValue<Organization>(_context, EntityId, "sponsor");
            Producer_as_Organization = new IdentifierValue<Organization>(_context, EntityId, "producer");
            Producer_as_Person = new IdentifierValue<Person>(_context, EntityId, "producer");
            Mentions = new IdentifierValue<Thing>(_context, EntityId, "mentions");
            DateCreated = new TimeValue(_context, EntityId, "dateCreated");
            DatePublished = new TimeValue(_context, EntityId, "datePublished");
            IsAccessibleForFree = new BooleanValue(_context, EntityId, "isAccessibleForFree");
            Headline = new TextValue(_context, EntityId, "headline");
            Citation_as_CreativeWork = new IdentifierValue<CreativeWork>(_context, EntityId, "citation");
            Citation_as_string = new TextValue(_context, EntityId, "citation");
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
        /// <para>(From http://schema.org/Movie)</para>
        /// <para>An actor, e.g. in tv, radio, movie, video games etc., or in an event. Actors can be associated with individual items or with a series, episode, clip.</para>
        /// </summary>
        public IdentifierValue<Person> Actor { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Movie)</para>
        /// <para>The trailer of a movie or tv/radio series, season, episode, etc.</para>
        /// </summary>
        public IdentifierValue<Entity> Trailer { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Movie)</para>
        /// <para>Languages in which subtitles/captions are available, in &lt;a href="http://tools.ietf.org/html/bcp47"&gt;IETF BCP 47 standard format&lt;/a&gt;.</para>
        /// </summary>
        public IdentifierValue<Entity> SubtitleLanguage_as_Language { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Movie)</para>
        /// <para>Languages in which subtitles/captions are available, in &lt;a href="http://tools.ietf.org/html/bcp47"&gt;IETF BCP 47 standard format&lt;/a&gt;.</para>
        /// </summary>
        public TextValue SubtitleLanguage_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Movie)</para>
        /// <para>The country of the principal offices of the production company or individual responsible for the movie or program.</para>
        /// </summary>
        public IdentifierValue<Entity> CountryOfOrigin { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Movie)</para>
        /// <para>The composer of the soundtrack.</para>
        /// </summary>
        public IdentifierValue<Entity> MusicBy_as_MusicGroup { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Movie)</para>
        /// <para>The composer of the soundtrack.</para>
        /// </summary>
        public IdentifierValue<Person> MusicBy_as_Person { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Movie)</para>
        /// <para>A director of e.g. tv, radio, movie, video games etc. content. Directors can be associated with individual items or with a series, episode, clip.</para>
        /// </summary>
        public IdentifierValue<Person> Directors { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Movie)</para>
        /// <para>A director of e.g. tv, radio, movie, video gaming etc. content, or of an event. Directors can be associated with individual items or with a series, episode, clip.</para>
        /// </summary>
        public IdentifierValue<Person> Director { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Movie)</para>
        /// <para>The production company or studio responsible for the item e.g. series, video game, episode etc.</para>
        /// </summary>
        public IdentifierValue<Organization> ProductionCompany { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Movie)</para>
        /// <para>The duration of the item (movie, audio recording, event, etc.) in &lt;a href="http://en.wikipedia.org/wiki/ISO_8601"&gt;ISO 8601 date format&lt;/a&gt;.</para>
        /// </summary>
        public IdentifierValue<Entity> Duration { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Movie)</para>
        /// <para>An actor, e.g. in tv, radio, movie, video games etc. Actors can be associated with individual items or with a series, episode, clip.</para>
        /// </summary>
        public IdentifierValue<Person> Actors { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The subject matter of the content.</para>
        /// </summary>
        public IdentifierValue<Thing> About { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>An alignment to an established educational framework.</para>
        /// </summary>
        public IdentifierValue<Entity> EducationalAlignment { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A media object that encodes this CreativeWork. This property is a synonym for encoding.</para>
        /// </summary>
        public IdentifierValue<Entity> AssociatedMedia { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A person or organization that supports (sponsors) something through some kind of financial contribution.</para>
        /// </summary>
        public IdentifierValue<Person> Funder_as_Person { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A person or organization that supports (sponsors) something through some kind of financial contribution.</para>
        /// </summary>
        public IdentifierValue<Organization> Funder_as_Organization { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The position of an item in a series or sequence of items.</para>
        /// </summary>
        public IdentifierValue<Entity> Position_as_Integer { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The position of an item in a series or sequence of items.</para>
        /// </summary>
        public TextValue Position_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>An embedded audio object.</para>
        /// </summary>
        public IdentifierValue<Entity> Audio { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>Example/instance/realization/derivation of the concept of this creative work. eg. The paperback edition, first edition, or eBook.</para>
        /// </summary>
        public IdentifierValue<CreativeWork> WorkExample { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The service provider, service operator, or service performer; the goods producer. Another party (a seller) may offer those services or goods on behalf of the provider. A provider may also serve as the seller.</para>
        /// </summary>
        public IdentifierValue<Organization> Provider_as_Organization { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The service provider, service operator, or service performer; the goods producer. Another party (a seller) may offer those services or goods on behalf of the provider. A provider may also serve as the seller.</para>
        /// </summary>
        public IdentifierValue<Person> Provider_as_Person { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A media object that encodes this CreativeWork. This property is a synonym for associatedMedia.</para>
        /// </summary>
        public IdentifierValue<Entity> Encoding { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The predominant mode of learning supported by the learning resource. Acceptable values are &apos;active&apos;, &apos;expositive&apos;, or &apos;mixed&apos;.</para>
        /// </summary>
        public TextValue InteractivityType { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A human-readable summary of specific accessibility features or deficiencies, consistent with the other accessibility metadata but expressing subtleties such as "short descriptions are present but long descriptions will be needed for non-visual users" or "short descriptions are present and no long descriptions are needed."</para>
        /// </summary>
        public TextValue AccessibilitySummary { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>Fictional person connected with a creative work.</para>
        /// </summary>
        public IdentifierValue<Person> Character { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>An intended audience, i.e. a group for whom something was created.</para>
        /// </summary>
        public IdentifierValue<Entity> Audience { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The Organization on whose behalf the creator was working.</para>
        /// </summary>
        public IdentifierValue<Organization> SourceOrganization { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>Indicates a CreativeWork that this CreativeWork is (in some sense) part of.</para>
        /// </summary>
        public IdentifierValue<CreativeWork> IsPartOf { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>An embedded video object.</para>
        /// </summary>
        public IdentifierValue<Entity> Video { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The publisher of the creative work.</para>
        /// </summary>
        public IdentifierValue<Organization> Publisher_as_Organization { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The publisher of the creative work.</para>
        /// </summary>
        public IdentifierValue<Person> Publisher_as_Person { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A publication event associated with the item.</para>
        /// </summary>
        public IdentifierValue<Entity> Publication { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The textual content of this CreativeWork.</para>
        /// </summary>
        public TextValue Text { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>Date the content expires and is no longer useful or available. For example a &lt;a class="localLink" href="http://schema.org/VideoObject"&gt;VideoObject&lt;/a&gt; or &lt;a class="localLink" href="http://schema.org/NewsArticle"&gt;NewsArticle&lt;/a&gt; whose availability or relevance is time-limited, or a &lt;a class="localLink" href="http://schema.org/ClaimReview"&gt;ClaimReview&lt;/a&gt; fact check whose publisher wants to indicate that it may no longer be relevant (or helpful to highlight) after some date.</para>
        /// </summary>
        public TimeValue Expires { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A secondary contributor to the CreativeWork or Event.</para>
        /// </summary>
        public IdentifierValue<Person> Contributor_as_Person { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A secondary contributor to the CreativeWork or Event.</para>
        /// </summary>
        public IdentifierValue<Organization> Contributor_as_Organization { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>Review of the item.</para>
        /// </summary>
        public IdentifierValue<Entity> Reviews { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The typical expected age range, e.g. &apos;7-9&apos;, &apos;11-&apos;.</para>
        /// </summary>
        public TextValue TypicalAgeRange { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The place and time the release was issued, expressed as a PublicationEvent.</para>
        /// </summary>
        public IdentifierValue<Entity> ReleasedEvent { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The purpose of a work in the context of education; for example, &apos;assignment&apos;, &apos;group work&apos;.</para>
        /// </summary>
        public TextValue EducationalUse { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The location depicted or described in the content. For example, the location in a photograph or painting.</para>
        /// </summary>
        public IdentifierValue<Place> ContentLocation { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>Indicates (by URL or string) a particular version of a schema used in some CreativeWork. For example, a document could declare a schemaVersion using an URL such as http://schema.org/version/2.0/ if precise indication of schema version was required by some application.</para>
        /// </summary>
        public TextValue SchemaVersion_as_URL { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>Indicates (by URL or string) a particular version of a schema used in some CreativeWork. For example, a document could declare a schemaVersion using an URL such as http://schema.org/version/2.0/ if precise indication of schema version was required by some application.</para>
        /// </summary>
        public TextValue SchemaVersion_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>Content features of the resource, such as accessible media, alternatives and supported enhancements for accessibility (&lt;a href="http://www.w3.org/wiki/WebSchemas/Accessibility"&gt;WebSchemas wiki lists possible values&lt;/a&gt;).</para>
        /// </summary>
        public TextValue AccessibilityFeature { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The overall rating, based on a collection of reviews or ratings, of the item.</para>
        /// </summary>
        public IdentifierValue<Entity> AggregateRating { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A secondary title of the CreativeWork.</para>
        /// </summary>
        public TextValue AlternativeHeadline { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The location where the CreativeWork was created, which may not be the same as the location depicted in the CreativeWork.</para>
        /// </summary>
        public IdentifierValue<Place> LocationCreated { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A list of single or combined accessModes that are sufficient to understand all the intellectual content of a resource. Expected values include:  auditory, tactile, textual, visual.</para>
        /// </summary>
        public TextValue AccessModeSufficient { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The temporalCoverage of a CreativeWork indicates the period that the content applies to, i.e. that it describes, either as a DateTime or as a textual string indicating a time period in &lt;a href="https://en.wikipedia.org/wiki/ISO_8601#Time_intervals"&gt;ISO 8601 time interval format&lt;/a&gt;. In</para>
        /// <para>      the case of a Dataset it will typically indicate the relevant time period in a precise notation (e.g. for a 2011 census dataset, the year 2011 would be written "2011/2012"). Other forms of content e.g. ScholarlyArticle, Book, TVSeries or TVEpisode may indicate their temporalCoverage in broader terms - textually or via well-known URL.</para>
        /// <para>      Written works such as books may sometimes have precise temporal coverage too, e.g. a work set in 1939 - 1945 can be indicated in ISO 8601 interval format format via "1939/1945".</para>
        /// </summary>
        public TimeValue TemporalCoverage_as_time { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The temporalCoverage of a CreativeWork indicates the period that the content applies to, i.e. that it describes, either as a DateTime or as a textual string indicating a time period in &lt;a href="https://en.wikipedia.org/wiki/ISO_8601#Time_intervals"&gt;ISO 8601 time interval format&lt;/a&gt;. In</para>
        /// <para>      the case of a Dataset it will typically indicate the relevant time period in a precise notation (e.g. for a 2011 census dataset, the year 2011 would be written "2011/2012"). Other forms of content e.g. ScholarlyArticle, Book, TVSeries or TVEpisode may indicate their temporalCoverage in broader terms - textually or via well-known URL.</para>
        /// <para>      Written works such as books may sometimes have precise temporal coverage too, e.g. a work set in 1939 - 1945 can be indicated in ISO 8601 interval format format via "1939/1945".</para>
        /// </summary>
        public TextValue TemporalCoverage_as_URL { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The temporalCoverage of a CreativeWork indicates the period that the content applies to, i.e. that it describes, either as a DateTime or as a textual string indicating a time period in &lt;a href="https://en.wikipedia.org/wiki/ISO_8601#Time_intervals"&gt;ISO 8601 time interval format&lt;/a&gt;. In</para>
        /// <para>      the case of a Dataset it will typically indicate the relevant time period in a precise notation (e.g. for a 2011 census dataset, the year 2011 would be written "2011/2012"). Other forms of content e.g. ScholarlyArticle, Book, TVSeries or TVEpisode may indicate their temporalCoverage in broader terms - textually or via well-known URL.</para>
        /// <para>      Written works such as books may sometimes have precise temporal coverage too, e.g. a work set in 1939 - 1945 can be indicated in ISO 8601 interval format format via "1939/1945".</para>
        /// </summary>
        public TextValue TemporalCoverage_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>Specifies the Person that is legally accountable for the CreativeWork.</para>
        /// </summary>
        public IdentifierValue<Person> AccountablePerson { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The spatialCoverage of a CreativeWork indicates the place(s) which are the focus of the content. It is a subproperty of</para>
        /// <para>      contentLocation intended primarily for more technical and detailed materials. For example with a Dataset, it indicates</para>
        /// <para>      areas that the dataset describes: a dataset of New York weather would have spatialCoverage which was the place: the state of New York.</para>
        /// </summary>
        public IdentifierValue<Place> SpatialCoverage { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>An offer to provide this item&amp;#x2014;for example, an offer to sell a product, rent the DVD of a movie, perform a service, or give away tickets to an event.</para>
        /// </summary>
        public IdentifierValue<Entity> Offers { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>Specifies the Person who edited the CreativeWork.</para>
        /// </summary>
        public IdentifierValue<Person> Editor { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A link to the page containing the comments of the CreativeWork.</para>
        /// </summary>
        public TextValue DiscussionUrl { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>An award won by or for this item.</para>
        /// </summary>
        public TextValue Award { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The party holding the legal copyright to the CreativeWork.</para>
        /// </summary>
        public IdentifierValue<Organization> CopyrightHolder_as_Organization { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The party holding the legal copyright to the CreativeWork.</para>
        /// </summary>
        public IdentifierValue<Person> CopyrightHolder_as_Person { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A characteristic of the described resource that is physiologically dangerous to some users. Related to WCAG 2.0 guideline 2.3 (&lt;a href="http://www.w3.org/wiki/WebSchemas/Accessibility"&gt;WebSchemas wiki lists possible values&lt;/a&gt;).</para>
        /// </summary>
        public TextValue AccessibilityHazard { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The year during which the claimed copyright for the CreativeWork was first asserted.</para>
        /// </summary>
        public NumberValue CopyrightYear { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>Awards won by or for this item.</para>
        /// </summary>
        public TextValue Awards { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The Event where the CreativeWork was recorded. The CreativeWork may capture all or part of the event.</para>
        /// </summary>
        public IdentifierValue<Entity> RecordedAt { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The number of comments this CreativeWork (e.g. Article, Question or Answer) has received. This is most applicable to works published in Web sites with commenting system; additional comments may exist elsewhere.</para>
        /// </summary>
        public IdentifierValue<Entity> CommentCount { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>Media type, typically MIME format (see &lt;a href="http://www.iana.org/assignments/media-types/media-types.xhtml"&gt;IANA site&lt;/a&gt;) of the content e.g. application/zip of a SoftwareApplication binary. In cases where a CreativeWork has several media type representations, &apos;encoding&apos; can be used to indicate each MediaObject alongside particular fileFormat information. Unregistered or niche file formats can be indicated instead via the most appropriate URL, e.g. defining Web page or a Wikipedia entry.</para>
        /// </summary>
        public TextValue FileFormat_as_URL { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>Media type, typically MIME format (see &lt;a href="http://www.iana.org/assignments/media-types/media-types.xhtml"&gt;IANA site&lt;/a&gt;) of the content e.g. application/zip of a SoftwareApplication binary. In cases where a CreativeWork has several media type representations, &apos;encoding&apos; can be used to indicate each MediaObject alongside particular fileFormat information. Unregistered or niche file formats can be indicated instead via the most appropriate URL, e.g. defining Web page or a Wikipedia entry.</para>
        /// </summary>
        public TextValue FileFormat_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The language of the content or performance or used in an action. Please use one of the language codes from the &lt;a href="http://tools.ietf.org/html/bcp47"&gt;IETF BCP 47 standard&lt;/a&gt;. See also &lt;a class="localLink" href="http://schema.org/availableLanguage"&gt;availableLanguage&lt;/a&gt;.</para>
        /// </summary>
        public IdentifierValue<Entity> InLanguage_as_Language { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The language of the content or performance or used in an action. Please use one of the language codes from the &lt;a href="http://tools.ietf.org/html/bcp47"&gt;IETF BCP 47 standard&lt;/a&gt;. See also &lt;a class="localLink" href="http://schema.org/availableLanguage"&gt;availableLanguage&lt;/a&gt;.</para>
        /// </summary>
        public TextValue InLanguage_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>Indicates that the resource is compatible with the referenced accessibility API (&lt;a href="http://www.w3.org/wiki/WebSchemas/Accessibility"&gt;WebSchemas wiki lists possible values&lt;/a&gt;).</para>
        /// </summary>
        public TextValue AccessibilityAPI { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The number of interactions for the CreativeWork using the WebSite or SoftwareApplication. The most specific child type of InteractionCounter should be used.</para>
        /// </summary>
        public IdentifierValue<Entity> InteractionStatistic { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>Official rating of a piece of content&amp;#x2014;for example,&apos;MPAA PG-13&apos;.</para>
        /// </summary>
        public TextValue ContentRating { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The predominant type or kind characterizing the learning resource. For example, &apos;presentation&apos;, &apos;handout&apos;.</para>
        /// </summary>
        public TextValue LearningResourceType { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The human sensory perceptual system or cognitive faculty through which a person may process or perceive information. Expected values include: auditory, tactile, textual, visual, colorDependent, chartOnVisual, chemOnVisual, diagramOnVisual, mathOnVisual, musicOnVisual, textOnVisual.</para>
        /// </summary>
        public TextValue AccessMode { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A material that something is made from, e.g. leather, wool, cotton, paper.</para>
        /// </summary>
        public TextValue Material_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A material that something is made from, e.g. leather, wool, cotton, paper.</para>
        /// </summary>
        public TextValue Material_as_URL { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A material that something is made from, e.g. leather, wool, cotton, paper.</para>
        /// </summary>
        public IdentifierValue<Entity> Material_as_Product { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>Indicates whether this content is family friendly.</para>
        /// </summary>
        public BooleanValue IsFamilyFriendly { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A creative work that this work is an example/instance/realization/derivation of.</para>
        /// </summary>
        public IdentifierValue<CreativeWork> ExampleOfWork { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The version of the CreativeWork embodied by a specified resource.</para>
        /// </summary>
        public TextValue Version_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The version of the CreativeWork embodied by a specified resource.</para>
        /// </summary>
        public NumberValue Version_as_number { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The date on which the CreativeWork was most recently modified or when the item&apos;s entry was modified within a DataFeed.</para>
        /// </summary>
        public TimeValue DateModified { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>Indicates the primary entity described in some page or other CreativeWork.</para>
        /// </summary>
        public IdentifierValue<Thing> MainEntity { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>Genre of the creative work, broadcast channel or group.</para>
        /// </summary>
        public TextValue Genre_as_URL { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>Genre of the creative work, broadcast channel or group.</para>
        /// </summary>
        public TextValue Genre_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>Keywords or tags used to describe this content. Multiple entries in a keywords list are typically delimited by commas.</para>
        /// </summary>
        public TextValue Keywords { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The author of this content or rating. Please note that author is special in that HTML 5 provides a special mechanism for indicating authorship via the rel tag. That is equivalent to this and may be used interchangeably.</para>
        /// </summary>
        public IdentifierValue<Organization> Author_as_Organization { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The author of this content or rating. Please note that author is special in that HTML 5 provides a special mechanism for indicating authorship via the rel tag. That is equivalent to this and may be used interchangeably.</para>
        /// </summary>
        public IdentifierValue<Person> Author_as_Person { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A resource that was used in the creation of this resource. This term can be repeated for multiple sources. For example, http://example.com/great-multiplication-intro.html.</para>
        /// </summary>
        public IdentifierValue<Entity> IsBasedOnUrl_as_Product { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A resource that was used in the creation of this resource. This term can be repeated for multiple sources. For example, http://example.com/great-multiplication-intro.html.</para>
        /// </summary>
        public IdentifierValue<CreativeWork> IsBasedOnUrl_as_CreativeWork { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A resource that was used in the creation of this resource. This term can be repeated for multiple sources. For example, http://example.com/great-multiplication-intro.html.</para>
        /// </summary>
        public TextValue IsBasedOnUrl_as_URL { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>Approximate or typical time it takes to work with or through this learning resource for the typical intended target audience, e.g. &apos;P30M&apos;, &apos;P1H25M&apos;.</para>
        /// </summary>
        public IdentifierValue<Entity> TimeRequired { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>Organization or person who adapts a creative work to different languages, regional differences and technical requirements of a target market, or that translates during some event.</para>
        /// </summary>
        public IdentifierValue<Organization> Translator_as_Organization { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>Organization or person who adapts a creative work to different languages, regional differences and technical requirements of a target market, or that translates during some event.</para>
        /// </summary>
        public IdentifierValue<Person> Translator_as_Person { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A thumbnail image relevant to the Thing.</para>
        /// </summary>
        public TextValue ThumbnailUrl { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>Indicates a CreativeWork that is (in some sense) a part of this CreativeWork.</para>
        /// </summary>
        public IdentifierValue<CreativeWork> HasPart { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>Comments, typically from users.</para>
        /// </summary>
        public IdentifierValue<Entity> Comment { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A review of the item.</para>
        /// </summary>
        public IdentifierValue<Entity> Review { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A license document that applies to this content, typically indicated by URL.</para>
        /// </summary>
        public IdentifierValue<CreativeWork> License_as_CreativeWork { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A license document that applies to this content, typically indicated by URL.</para>
        /// </summary>
        public TextValue License_as_URL { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>Identifies input methods that are sufficient to fully control the described resource (&lt;a href="http://www.w3.org/wiki/WebSchemas/Accessibility"&gt;WebSchemas wiki lists possible values&lt;/a&gt;).</para>
        /// </summary>
        public TextValue AccessibilityControl { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A media object that encodes this CreativeWork.</para>
        /// </summary>
        public IdentifierValue<Entity> Encodings { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A resource that was used in the creation of this resource. This term can be repeated for multiple sources. For example, http://example.com/great-multiplication-intro.html.</para>
        /// </summary>
        public IdentifierValue<Entity> IsBasedOn_as_Product { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A resource that was used in the creation of this resource. This term can be repeated for multiple sources. For example, http://example.com/great-multiplication-intro.html.</para>
        /// </summary>
        public IdentifierValue<CreativeWork> IsBasedOn_as_CreativeWork { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A resource that was used in the creation of this resource. This term can be repeated for multiple sources. For example, http://example.com/great-multiplication-intro.html.</para>
        /// </summary>
        public TextValue IsBasedOn_as_URL { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The creator/author of this CreativeWork. This is the same as the Author property for CreativeWork.</para>
        /// </summary>
        public IdentifierValue<Organization> Creator_as_Organization { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The creator/author of this CreativeWork. This is the same as the Author property for CreativeWork.</para>
        /// </summary>
        public IdentifierValue<Person> Creator_as_Person { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The publishingPrinciples property indicates (typically via &lt;a class="localLink" href="http://schema.org/URL"&gt;URL&lt;/a&gt;) a document describing the editorial principles of an &lt;a class="localLink" href="http://schema.org/Organization"&gt;Organization&lt;/a&gt; (or individual e.g. a &lt;a class="localLink" href="http://schema.org/Person"&gt;Person&lt;/a&gt; writing a blog) that relate to their activities as a publisher, e.g. ethics or diversity policies. When applied to a &lt;a class="localLink" href="http://schema.org/CreativeWork"&gt;CreativeWork&lt;/a&gt; (e.g. &lt;a class="localLink" href="http://schema.org/NewsArticle"&gt;NewsArticle&lt;/a&gt;) the principles are those of the party primarily responsible for the creation of the &lt;a class="localLink" href="http://schema.org/CreativeWork"&gt;CreativeWork&lt;/a&gt;.&lt;/p&gt;</para>
        /// <para>&lt;p&gt;While such policies are most typically expressed in natural language, sometimes related information (e.g. indicating a &lt;a class="localLink" href="http://schema.org/funder"&gt;funder&lt;/a&gt;) can be expressed using schema.org terminology.</para>
        /// </summary>
        public IdentifierValue<CreativeWork> PublishingPrinciples_as_CreativeWork { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The publishingPrinciples property indicates (typically via &lt;a class="localLink" href="http://schema.org/URL"&gt;URL&lt;/a&gt;) a document describing the editorial principles of an &lt;a class="localLink" href="http://schema.org/Organization"&gt;Organization&lt;/a&gt; (or individual e.g. a &lt;a class="localLink" href="http://schema.org/Person"&gt;Person&lt;/a&gt; writing a blog) that relate to their activities as a publisher, e.g. ethics or diversity policies. When applied to a &lt;a class="localLink" href="http://schema.org/CreativeWork"&gt;CreativeWork&lt;/a&gt; (e.g. &lt;a class="localLink" href="http://schema.org/NewsArticle"&gt;NewsArticle&lt;/a&gt;) the principles are those of the party primarily responsible for the creation of the &lt;a class="localLink" href="http://schema.org/CreativeWork"&gt;CreativeWork&lt;/a&gt;.&lt;/p&gt;</para>
        /// <para>&lt;p&gt;While such policies are most typically expressed in natural language, sometimes related information (e.g. indicating a &lt;a class="localLink" href="http://schema.org/funder"&gt;funder&lt;/a&gt;) can be expressed using schema.org terminology.</para>
        /// </summary>
        public TextValue PublishingPrinciples_as_URL { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A person or organization that supports a thing through a pledge, promise, or financial contribution. e.g. a sponsor of a Medical Study or a corporate sponsor of an event.</para>
        /// </summary>
        public IdentifierValue<Person> Sponsor_as_Person { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A person or organization that supports a thing through a pledge, promise, or financial contribution. e.g. a sponsor of a Medical Study or a corporate sponsor of an event.</para>
        /// </summary>
        public IdentifierValue<Organization> Sponsor_as_Organization { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The person or organization who produced the work (e.g. music album, movie, tv/radio series etc.).</para>
        /// </summary>
        public IdentifierValue<Organization> Producer_as_Organization { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The person or organization who produced the work (e.g. music album, movie, tv/radio series etc.).</para>
        /// </summary>
        public IdentifierValue<Person> Producer_as_Person { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>Indicates that the CreativeWork contains a reference to, but is not necessarily about a concept.</para>
        /// </summary>
        public IdentifierValue<Thing> Mentions { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>The date on which the CreativeWork was created or the item was added to a DataFeed.</para>
        /// </summary>
        public TimeValue DateCreated { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>Date of first broadcast/publication.</para>
        /// </summary>
        public TimeValue DatePublished { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A flag to signal that the item, event, or place is accessible for free.</para>
        /// </summary>
        public BooleanValue IsAccessibleForFree { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>Headline of the article.</para>
        /// </summary>
        public TextValue Headline { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A citation or reference to another creative work, such as another publication, web page, scholarly article, etc.</para>
        /// </summary>
        public IdentifierValue<CreativeWork> Citation_as_CreativeWork { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/CreativeWork)</para>
        /// <para>A citation or reference to another creative work, such as another publication, web page, scholarly article, etc.</para>
        /// </summary>
        public TextValue Citation_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>URL of a reference Web page that unambiguously indicates the item&apos;s identity. E.g. the URL of the item&apos;s Wikipedia page, Wikidata entry, or official website.</para>
        /// </summary>
        public TextValue SameAs { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>URL of the item.</para>
        /// </summary>
        public TextValue Url { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>An image of the item. This can be a &lt;a class="localLink" href="http://schema.org/URL"&gt;URL&lt;/a&gt; or a fully described &lt;a class="localLink" href="http://schema.org/ImageObject"&gt;ImageObject&lt;/a&gt;.</para>
        /// </summary>
        public IdentifierValue<Entity> Image_as_ImageObject { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>An image of the item. This can be a &lt;a class="localLink" href="http://schema.org/URL"&gt;URL&lt;/a&gt; or a fully described &lt;a class="localLink" href="http://schema.org/ImageObject"&gt;ImageObject&lt;/a&gt;.</para>
        /// </summary>
        public TextValue Image_as_URL { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>An additional type for the item, typically used for adding more specific types from external vocabularies in microdata syntax. This is a relationship between something and a class that the thing is in. In RDFa syntax, it is better to use the native RDFa syntax - the &apos;typeof&apos; attribute - for multiple types. Schema.org tools may have only weaker understanding of extra types, in particular those defined externally.</para>
        /// </summary>
        public TextValue AdditionalType { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>The name of the item.</para>
        /// </summary>
        public TextValue Name { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>The identifier property represents any kind of identifier for any kind of &lt;a class="localLink" href="http://schema.org/Thing"&gt;Thing&lt;/a&gt;, such as ISBNs, GTIN codes, UUIDs etc. Schema.org provides dedicated properties for representing many of these, either as textual strings or as URL (URI) links. See &lt;a href="/docs/datamodel.html#identifierBg"&gt;background notes&lt;/a&gt; for more details.</para>
        /// </summary>
        public IdentifierValue<Entity> Identifier_as_PropertyValue { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>The identifier property represents any kind of identifier for any kind of &lt;a class="localLink" href="http://schema.org/Thing"&gt;Thing&lt;/a&gt;, such as ISBNs, GTIN codes, UUIDs etc. Schema.org provides dedicated properties for representing many of these, either as textual strings or as URL (URI) links. See &lt;a href="/docs/datamodel.html#identifierBg"&gt;background notes&lt;/a&gt; for more details.</para>
        /// </summary>
        public TextValue Identifier_as_string { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>The identifier property represents any kind of identifier for any kind of &lt;a class="localLink" href="http://schema.org/Thing"&gt;Thing&lt;/a&gt;, such as ISBNs, GTIN codes, UUIDs etc. Schema.org provides dedicated properties for representing many of these, either as textual strings or as URL (URI) links. See &lt;a href="/docs/datamodel.html#identifierBg"&gt;background notes&lt;/a&gt; for more details.</para>
        /// </summary>
        public TextValue Identifier_as_URL { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>Indicates a potential Action, which describes an idealized action in which this thing would play an &apos;object&apos; role.</para>
        /// </summary>
        public IdentifierValue<Entity> PotentialAction { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>Indicates a page (or other CreativeWork) for which this thing is the main entity being described. See &lt;a href="/docs/datamodel.html#mainEntityBackground"&gt;background notes&lt;/a&gt; for details.</para>
        /// </summary>
        public TextValue MainEntityOfPage_as_URL { get; private set; }
        /// <summary>
        /// <para>(From http://schema.org/Thing)</para>
        /// <para>Indicates a page (or other CreativeWork) for which this thing is the main entity being described. See &lt;a href="/docs/datamodel.html#mainEntityBackground"&gt;background notes&lt;/a&gt; for details.</para>
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
