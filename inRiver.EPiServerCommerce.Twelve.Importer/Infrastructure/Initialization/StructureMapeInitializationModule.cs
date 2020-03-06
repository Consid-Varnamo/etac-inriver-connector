using System.Web.Http;
using EPiServer.Framework;
using EPiServer.Framework.Initialization;
using EPiServer.ServiceLocation;
using inRiver.EPiServerCommerce.Twelve.Importer.Infrastructure.StructureMap;
using StructureMap;

namespace Etac.Infrastructure.Initialization
{
    [InitializableModule]
    public class StructureMapInitializationModule : IConfigurableModule
    {
        private IContainer container;

        public void ConfigureContainer(ServiceConfigurationContext context)
        {
            container = context.StructureMap();
            GlobalConfiguration.Configuration.DependencyResolver = new WebApiDependencyResolver(container);
        }

        public void Initialize(InitializationEngine context)
        {
            StructureMapConfiguration.Configure(container);
        }

        public void Uninitialize(InitializationEngine context)
        {
        }
    }
}
