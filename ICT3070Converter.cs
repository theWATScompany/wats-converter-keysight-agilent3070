using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Virinco.WATS.Interface;

namespace Virinco.WATS.Converter.Keysight
{
    public class Agilent3070Converter : IReportConverter_v2
    {
        string par_stationname = string.Empty;
        string par_location = string.Empty;
        string par_purpose = string.Empty;
        string par_sequencefile = string.Empty;
        string par_sequenceversion = string.Empty;
        string par_operationcode = string.Empty;

        string[] splitter = new string[] { "|" };

        UUTReport uut = null;
        Step CurrentStep = null;
        SequenceCall CurrentSequenceCall = null;
        System.Globalization.CultureInfo provider = System.Globalization.CultureInfo.InvariantCulture;

        protected IDictionary<string, string> converterArguments;

        // IReportConverter_v2 parameters property
        Dictionary<string, string> parameters = new Dictionary<string, string>();
        public Dictionary<string, string> ConverterParameters => parameters;

        public Agilent3070Converter()
        {
            // Default parameters similar to previous GetParameters
            parameters = new Dictionary<string, string>()
            {
                { "operationTypeCode", "10" },
                { "stationname", "test-machine" },
                { "sequenceFile", "seqName" },
                { "sequenceVersion", "1.0.0" },
            };
            InitializeFromParameters(parameters);
        }

        public Agilent3070Converter(IDictionary<string, string> args)
        {
            //Setup default from Converter.xml arguments
            converterArguments = args;
            parameters = args != null ? new Dictionary<string, string>(args) : new Dictionary<string, string>();
            InitializeFromParameters(parameters);
        }

        private void InitializeFromParameters(IDictionary<string, string> args)
        {
            par_operationcode = GetStringFromDictionary(args, "operationTypeCode", "10");
            par_stationname = GetStringFromDictionary(args, "stationname", "");
            par_sequencefile = GetStringFromDictionary(args, "sequenceFile", "");
            par_sequenceversion = GetStringFromDictionary(args, "sequenceVersion", "");
        }

        public static IDictionary<string, string> GetParameters()
        {
            return new Dictionary<string, string>()
            {
                { "operationTypeCode", "10" },
                { "stationname", "test-machine" },
                { "sequenceFile", "seqName" },
                { "sequenceVersion", "1.0.0" }
            };
        }

        public Report ImportReport(TDM api, System.IO.Stream file)
        {
            api.TestMode = TestModeType.Active;
            int lineCount = 0;
            UUTStatusType testStatus = UUTStatusType.Error;
            try
            {
                using (StreamReader reader = new StreamReader(file))
                {
                    String line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!string.IsNullOrEmpty(line))
                        {
                            string test = line.TrimStart().ToLower();

                            string[] rowValues = line.Split(splitter, StringSplitOptions.None);

                            if (uut == null)
                            {
                                uut = processHeader(api, rowValues);
                                testStatus = uut.Status;
                            }
                            else if (line.TrimStart().ToLower().StartsWith("pins|"))
                                ProcessBool(rowValues);

                            else if (line.TrimStart().ToLower().StartsWith("preshorts|"))
                                ProcessNumeric(rowValues);

                            else if (line.TrimStart().ToLower().StartsWith("shorts|"))
                                ProcessBool(rowValues);

                            else if (line.TrimStart().ToLower().StartsWith("analog unpowered|"))
                                ProcessNumeric(rowValues);

                            else if (line.TrimStart().ToLower().StartsWith("power supplies|"))
                                ProcessBool(rowValues);

                            else if (line.TrimStart().ToLower().StartsWith("digital|"))
                                ProcessBool(rowValues);

                            else if (line.TrimStart().ToLower().StartsWith("analog powered|"))
                                ProcessNumeric(rowValues);

                            else if (line.TrimStart().ToLower().StartsWith("miscellaneous data|"))
                                ProcessMiscellaneousData(rowValues);



                            else
                            {
                                string type = getValue(3, rowValues).ToLower().Trim();
                                if (type == "double" || type == "float" || type == "integer")
                                    ProcessNumeric(rowValues);
                                else if (type == "text" || type == "string")
                                    ProcessString(rowValues);
                                else if (type == "boolean")
                                    ProcessBool(rowValues);
                                else
                                    throw new Exception("Unknown testype: " + line);
                            }
                        }
                    }
                }
                uut.Status = testStatus;
                api.Submit(SubmitMethod.Offline, uut);
            }
            catch (Exception ex)
            {
                StreamWriter errorlog = new StreamWriter(api.ConversionSource.ErrorLog);
                errorlog.WriteLine("Error in file {0}, line {1}: {2}", api.ConversionSource.SourceFile.Name, lineCount, ex.Message);
                errorlog.Flush();
                throw;
            }
            return null;
        }

        public static Int16 ParseInt16(object obj, Int16 defaultValue)
        {
            try { return System.Convert.ToInt16(obj); }
            catch { return defaultValue; }
        }


        private void ProcessMiscellaneousData(string[] rowValues)
        {

            string name = getValue(1, rowValues);
            string string_value = getValue(2, rowValues);

            if (string.IsNullOrEmpty(string_value))
            {
                short numeric_value = ParseInt16(getValue(3, rowValues), short.MinValue);
                if (numeric_value != short.MinValue)
                {
                    string_value = numeric_value.ToString();
                }
                else
                {
                    string_value = string.Empty;
                }

            }
            ;

            string typedef = getValue(4, rowValues);
            MiscUUTInfo mi = uut.AddMiscUUTInfo(name, string_value);


        }

        #region NumericStringBool_StepParsers

        int measureCounter = 1;
        private void ProcessNumeric(string[] rowValues)
        {
            String tmp = string.Empty;

            string sequenceCallName = getValue(0, rowValues);
            string StepName = getValue(1, rowValues);
            string stepId = getValue(2, rowValues); //NA
            string datatype = getValue(3, rowValues).ToLower().Trim();

            tmp = getValue(4, rowValues);
            double value = double.NaN;
            double.TryParse(tmp, System.Globalization.NumberStyles.Any, provider, out value);

            tmp = getValue(6, rowValues);
            double min = double.NaN;
            double.TryParse(tmp, System.Globalization.NumberStyles.Any, provider, out min);

            tmp = getValue(7, rowValues);
            double max = double.NaN;
            double.TryParse(tmp, System.Globalization.NumberStyles.Any, provider, out max);

            string CompOp = getValue(8, rowValues); //NA
            string Units = getValue(9, rowValues);

            tmp = getValue(10, rowValues);
            StepStatusType status = GetStepStatusType(tmp);

            string steptime = getValue(11, rowValues); //NA

            string steptext = getValue(12, rowValues);

            string errorcode = getValue(13, rowValues);
            string errormessage = getValue(14, rowValues);
            string looping = getValue(15, rowValues);

            SequenceCall sc = GetSequenceCall(sequenceCallName);

            if (CurrentStep is NumericLimitStep && ((NumericLimitStep)CurrentStep).Name == StepName)
            {
                measureCounter++;
                CurrentStep.ReportText = "";
            }
            else
            {
                CurrentStep = sc.AddNumericLimitStep(StepName);
                measureCounter = 1;
                CurrentStep.ReportText = steptext.Truncate(3999);
            }
            NumericLimitStep nls = (NumericLimitStep)CurrentStep;
            string measName = String.Format("Meas {0}: {1}", measureCounter, steptext);
            if (measName.Length > 99)
                measName = measName.Truncate(96) + "...";
            NumericLimitTest nlt = nls.AddMultipleTest(value, CompOperatorType.GELE, min, max, Units, measName);
            nlt.MeasureStatus = status;

            if (status == StepStatusType.Failed)
            {
                nls.Status = status;
                sc.Status = StepStatusType.Failed;
            }
        }

        private void ProcessBool(string[] rowValues)
        {
            String tmp = string.Empty;

            string sequenceCallName = getValue(0, rowValues);
            string StepName = getValue(1, rowValues);
            string stepId = getValue(2, rowValues);
            string datatype = getValue(3, rowValues).ToLower().Trim();


            bool result = false;
            tmp = getValue(10, rowValues);
            result = tmp.Trim().ToLower() == "pass" ? true : false;
            tmp = getValue(4, rowValues);
            if (!string.IsNullOrEmpty(tmp))
                Boolean.TryParse(tmp, out result);


            string CompOp = getValue(8, rowValues);
            string Units = getValue(9, rowValues);

            tmp = getValue(10, rowValues);
            StepStatusType status = GetStepStatusType(tmp);

            string steptime = getValue(11, rowValues);

            string steptext = getValue(12, rowValues);

            string errorcode = getValue(13, rowValues);
            string errormessage = getValue(14, rowValues);
            string looping = getValue(15, rowValues);

            SequenceCall sc = GetSequenceCall(sequenceCallName);
            if (CurrentStep is PassFailStep && ((PassFailStep)CurrentStep).Name == StepName)
            {
                measureCounter++;
                CurrentStep.ReportText = "";
            }
            else
            {
                CurrentStep = sc.AddPassFailStep(StepName);
                measureCounter = 1;
                CurrentStep.ReportText = steptext.Truncate(3999);
            }

            string measName = String.Format("Meas {0}: {1}", measureCounter, steptext);
            if (measName.Length > 99)
                measName = measName.Truncate(96) + "...";
            PassFailStep pfs = (PassFailStep)CurrentStep;
            PassFailTest pft = pfs.AddMultipleTest(result, measName);
            pfs.StepErrorMessage = errormessage.Truncate(199);
            if (!result)
                sc.Status = StepStatusType.Failed;
        }

        private void ProcessString(string[] rowValues)
        {
            String tmp = string.Empty;

            string sequenceCallName = getValue(0, rowValues);
            string StepName = getValue(1, rowValues);
            string stepId = getValue(2, rowValues);
            string datatype = getValue(3, rowValues).ToLower().Trim();
            string result = getValue(4, rowValues);

            string low = getValue(6, rowValues);
            string high = getValue(7, rowValues);

            string CompOp = getValue(8, rowValues);
            string Units = getValue(9, rowValues);

            tmp = getValue(10, rowValues);
            StepStatusType status = GetStepStatusType(tmp);

            string steptime = getValue(11, rowValues);

            string steptext = getValue(12, rowValues);
            string errorcode = getValue(13, rowValues);
            string errormessage = getValue(14, rowValues);
            string looping = getValue(15, rowValues);

            SequenceCall sc = GetSequenceCall(sequenceCallName);

            if (CurrentStep is StringValueStep && ((StringValueStep)CurrentStep).Name == StepName)
            {
                measureCounter++;
                CurrentStep.ReportText = null;
            }
            else
            {
                CurrentStep = sc.AddStringValueStep(StepName);
                measureCounter = 1;
                CurrentStep.ReportText = steptext;
            }
            string measName = String.Format("Meas {0}: {1}", measureCounter, steptext);
            if (measName.Length > 99)
                measName = measName.Truncate(96) + "...";
            StringValueStep svs = (StringValueStep)CurrentStep;
            StringValueTest svt = svs.AddMultipleTest(result, measName);
        }

        #endregion


        private UUTReport processHeader(TDM api, string[] rowValues)
        {
            String tmp = string.Empty;

            string SerialNumber = rowValues.Length > 0 ? rowValues[0] : "";
            string PartNumber = rowValues.Length > 1 ? rowValues[1] : "";
            string Revision = rowValues.Length > 2 ? rowValues[2] : "";
            string OperationTypeName = rowValues.Length > 3 ? rowValues[3] : "";
            tmp = rowValues.Length > 4 ? rowValues[4] : "";

            UUTStatusType TestStatus = UUTStatusType.Error;
            switch (tmp.ToLower().Trim())
            {
                case "p": TestStatus = UUTStatusType.Passed; break;
                case "f": TestStatus = UUTStatusType.Failed; break;
                default: break;
            }
            ;

            string ErrorMessage = rowValues.Length > 5 ? rowValues[5] : "";

            tmp = rowValues.Length > 6 ? rowValues[6] : "";
            DateTime StartTime = DateTime.Now;
            if (!DateTime.TryParseExact(tmp, "yyMMddHHmmss", provider, System.Globalization.DateTimeStyles.None, out StartTime)) { }

            tmp = rowValues.Length > 7 ? rowValues[7] : "";

            tmp = rowValues.Length > 8 ? rowValues[8] : "";
            Double ExecutionTime = 0;
            Double.TryParse(tmp, out ExecutionTime);

            string TestStation = rowValues.Length > 9 ? rowValues[9] : "";
            if (TestStation == string.Empty)
                TestStation = par_stationname;

            string Location = rowValues.Length > 10 ? rowValues[10] : "";
            if (Location == string.Empty)
                Location = par_location;

            string Purpose = rowValues.Length > 11 ? rowValues[11] : "";
            if (Purpose == string.Empty)
                Purpose = par_purpose;

            string TestOperator = rowValues.Length > 12 ? rowValues[12] : "";

            string SequenceFile = rowValues.Length > 13 ? rowValues[13] : "";
            if (SequenceFile == string.Empty)
                SequenceFile = par_sequencefile;

            string SequenceVersion = rowValues.Length > 14 ? rowValues[14] : "";
            if (SequenceVersion == string.Empty)
                SequenceVersion = par_sequenceversion;

            string BatchNumber = rowValues.Length > 15 ? rowValues[15] : "";

            tmp = rowValues.Length > 16 ? rowValues[16] : "-1";
            short TestSocket = -1;
            short.TryParse(tmp, out TestSocket);

            string Comment = rowValues.Length > 17 ? rowValues[17] : "";
            string FixtureId = rowValues.Length > 18 ? rowValues[18] : "";

            OperationType operation = api.GetOperationTypes().Where(ot => ot.Name == OperationTypeName).SingleOrDefault();
            if (operation == null)
                operation = api.GetOperationType(par_operationcode);



            api.StationName = TestStation;

            UUTReport uut = api.CreateUUTReport(TestOperator, PartNumber, Revision, SerialNumber, operation, SequenceFile, SequenceVersion);


            uut.ErrorMessage = ErrorMessage;
            uut.FixtureId = FixtureId;
            uut.Comment = Comment;
            uut.BatchSerialNumber = BatchNumber;
            uut.ExecutionTime = ExecutionTime;
            uut.StartDateTime = StartTime;
            uut.Status = TestStatus;
            uut.TestSocketIndex = TestSocket;

            return uut;
        }


        private StepStatusType GetStepStatusType(string value)
        {
            switch (value.ToLower().Trim())
            {
                case "p":
                case "pass": return StepStatusType.Passed;
                case "f":
                case "fail": return StepStatusType.Failed;
                default: return StepStatusType.Error;
            }
            ;
        }

        private string getValue(int idx, string[] array)
        {
            return array.Length > idx ? array[idx] : "";
        }

        private SequenceCall GetSequenceCall(string sequenceCallName)
        {
            if (CurrentSequenceCall == null || CurrentSequenceCall.Name != sequenceCallName)
            {
                CurrentSequenceCall = uut.GetRootSequenceCall().AddSequenceCall(sequenceCallName);
                CurrentSequenceCall.FailParentOnFail = false;
                CurrentStep = null;
            }
            return CurrentSequenceCall;
        }

        protected static string GetStringFromDictionary(IDictionary<string, string> dict, string key, string defaultValue)
        {
            if (dict != null && dict.ContainsKey(key))
                return dict[key];
            else
                return defaultValue;
        }


        public void CleanUp()
        {
            //throw new NotImplementedException();
        }
    }

    static class extensions
    {
        public static string Truncate(this string source, int length)
        {
            if (source.Length > length)
            {
                source = source.Substring(0, length);
            }
            return source;
        }
    }
}
