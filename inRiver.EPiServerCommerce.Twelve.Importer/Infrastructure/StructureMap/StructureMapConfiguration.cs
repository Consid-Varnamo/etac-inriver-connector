using EPiServer.Logging.Compatibility;
using inRiver.EPiServerCommerce.Twelve.Importer.Services;
using StructureMap;

namespace inRiver.EPiServerCommerce.Twelve.Importer.Infrastructure.StructureMap
{
    internal static class StructureMapConfiguration
    {
        private static readonly ILog _log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling", Justification = "this is a setup function, so ok in this case")]
        public static void Configure(IContainer container)
        {
            container.Configure(i =>
            {
                i.Scan(s =>
                {
                    s.AssemblyContainingType<MetaDataService>();
                    s.TheCallingAssembly();
                    s.WithDefaultConventions();
                    s.RegisterConcreteTypesAgainstTheFirstInterface();
                });
            });

            if (_log.IsDebugEnabled)
            {
                _log.Debug(container.WhatDoIHave());
            }
        }
    }
}
