using System.Diagnostics.CodeAnalysis;

namespace inRiver.Connectors.EPiServer.Communication
{
    public class SingletonEPiLock
    {
        private static SingletonEPiLock instance = new SingletonEPiLock();

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1409:RemoveUnnecessaryCode", Justification = "Reviewed. Suppression is OK here.")]
        static SingletonEPiLock()
        {
        }

        private SingletonEPiLock()
        {
        }

        public static SingletonEPiLock Instance
        {
            get
            {
                return instance ?? (instance = new SingletonEPiLock());
            }
        }
    }
}
