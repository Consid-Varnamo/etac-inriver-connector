using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace inRiver.EPiServerCommerce.Interfaces
{
    public class InRiverImportResourceInformation
    {
        public string fileNameInCloud;
        public List<InRiverImportResource> resources;

        public InRiverImportResourceInformation(string fileNameInCloud, List<InRiverImportResource> resources)
        {
            this.fileNameInCloud = fileNameInCloud;
            this.resources = resources;
        }
    }
}
