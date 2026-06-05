using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virinco.WATS.Interface;

namespace Virinco.WATS.Converter.Keysight
{
    [TestClass]
    public class ConverterTest : TDM
    {

        [TestMethod]
        public void SetupClient()
        {
            SetupAPI(null, "location", "purpose", true);
            RegisterClient("Your WATS instance url", "username", "password/token");
            InitializeAPI(true);
        }

        [TestMethod]
        public void TestICT3070Converter()
        {
            InitializeAPI(true);
            string fn = @"Data\testdata_SN654654865";
            var arguments = ICT3070Converter.GetParameters();
            ICT3070Converter converter = new ICT3070Converter(arguments);
            using (FileStream file = new FileStream(fn, FileMode.Open))
            {
                SetConversionSource(new FileInfo(fn), (Dictionary<string, string>)arguments, null);
                Report uut = converter.ImportReport(this, file);
            }
            SubmitPendingReports();
            
        }
    }
}
