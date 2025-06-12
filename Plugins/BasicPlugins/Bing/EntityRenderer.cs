using Durandal.Plugins.Bing.Views;
using Durandal.Common.Ontology;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SchemaDotOrg = Durandal.Plugins.Basic.SchemaDotOrg;

namespace Durandal.Plugins.Bing
{
    public static class EntityRenderer
    {
        public static string RenderText(Entity entity)
        {
            return GetEntityText(entity);
        }

        public static string RenderImage(Entity entity)
        {
            return GetEntityImage(entity);
        }

        public static string RenderDescription(Entity entity)
        {
            return GetEntityDescription(entity);
        }

        public static string RenderHtmlCard(Entity entity)
        {
            return new BasicEntityCard()
            {
                Description = GetEntityDescription(entity),
                TypeName = entity.EntityTypeName,
                EntityText = GetEntityText(entity),
                Image = GetEntityImage(entity)
            }.Render();
        }

        public static string RenderHtml(Entity entity)
        {
            SingleEntityView view = new SingleEntityView()
            {
                EntityDescription = GetEntityDescription(entity),
                EntityText = GetEntityText(entity),
                EntityImage = GetEntityImage(entity)
            };

            return view.Render();
        }

        private static string GetEntityText(Entity entity)
        {
            if (entity.IsA<SchemaDotOrg.Place>())
            {
                SchemaDotOrg.Place cast = entity.As<SchemaDotOrg.Place>();
                if (!string.IsNullOrEmpty(cast.Name.Value))
                {
                    return cast.Name.Value;
                }

                SchemaDotOrg.PostalAddress address = cast.Address_as_PostalAddress.ValueInMemory;
                if (address != null)
                {
                    return address.Name.Value ?? address.StreetAddress.Value ?? address.AddressLocality.Value ?? address.AddressRegion.Value ?? address.AddressCountry_as_string.Value ?? "Somewhere";
                }
            }
            else if (entity.IsA<SchemaDotOrg.Thing>())
            {
                SchemaDotOrg.Thing cast = entity.As<SchemaDotOrg.Thing>();
                if (!string.IsNullOrEmpty(cast.Name.Value))
                {
                    return cast.Name.Value;
                }
            }

            string typeName = entity.EntityTypeName ?? "(Null typename)";
            string guid = entity.EntityId ?? "(Null)";
            string desc = "Unknown entity";

            return string.Format("{0}-{1} {2}",
                typeName,
                guid.Substring(0, Math.Min(6, guid.Length)),
                desc.Substring(0, Math.Min(60, desc.Length)));
        }

        private static string GetEntityDescription(Entity entity)
        {
            if (entity.IsA<SchemaDotOrg.Thing>())
            {
                return entity.As<SchemaDotOrg.Thing>().Description.Value;
            }

            return string.Empty;
        }

        private static string GetEntityImage(Entity entity)
        {
            if (entity.IsA<SchemaDotOrg.Thing>())
            {
                return entity.As<SchemaDotOrg.Thing>().Image_as_URL.Value;
            }

            return string.Empty;
        }
    }
}
