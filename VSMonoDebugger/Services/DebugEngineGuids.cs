using System;

namespace VSMonoDebugger.Services
{
    public enum EngineType
    {
        //AD7Engine,
        //MonoEngine,
        XamarinEngine
    }

    public static class DebugEngineGuids
    {
        public readonly static EngineType UseAD7Engine = EngineType.XamarinEngine;

        //public const string MonoEngineString = "D78CF801-CE2A-499B-BF1F-C81742877A34";
        public const string AD7EngineString = "8BF3AB9F-3864-449A-93AB-E7B0935FC8F5";
        public const string XamarinEngineString = "9E1626AE-7DB7-4138-AC41-641D55CF9A4A";

        //public const string MonoProgramProviderString = "00171DED-5920-4ACD-93C2-BD9E4FA10CA0";
        public const string AD7ProgramProviderString = "CA171DED-5920-4ACD-93C2-BD9E4FA10CA0";
        public const string XamarinProgramProviderString = "B8291DF6-D514-4D91-8BE3-476FF244EFA9";

        public const string EngineName = "VSMonoDebugger";

        public static Guid ProgramProviderGuid
        {
            get
            {
                switch (UseAD7Engine)
                {
                    //case EngineType.AD7Engine:
                    //    return new Guid(AD7ProgramProviderString);
                    //case EngineType.MonoEngine:
                    //    return new Guid(MonoProgramProviderString);
                    case EngineType.XamarinEngine:
                        return new Guid(XamarinProgramProviderString);
                    default:
                        throw new NotSupportedException(UseAD7Engine.ToString());

                }
            }
        }
        public static Guid EngineGuid
        {
            get
            {
                switch (UseAD7Engine)
                {
                    //case EngineType.AD7Engine:
                    //    return new Guid(AD7EngineString);
                    //case EngineType.MonoEngine:
                    //    return new Guid(MonoEngineString);
                    case EngineType.XamarinEngine:
                        return new Guid(XamarinEngineString);
                    default:
                        throw new NotSupportedException(UseAD7Engine.ToString());
                }
            }
        }


        // Language guid for C++. Used when the language for a document context or a stack frame is requested.
        static private Guid s_guidLanguageCpp = new Guid("3a12d0b7-c26c-11d0-b442-00a0244a1dd2");
        static public Guid guidLanguageCpp
        {
            get { return s_guidLanguageCpp; }
        }

        static private Guid s_guidLanguageCs = new Guid("{3F5162F8-07C6-11D3-9053-00C04FA302A1}");
        static public Guid guidLanguageCs
        {
            get { return s_guidLanguageCs; }
        }

        static private Guid s_guidLanguageC = new Guid("63A08714-FC37-11D2-904C-00C04FA302A1");
        static public Guid guidLanguageC
        {
            get { return s_guidLanguageC; }
        }

        static private Guid s_guidFilterRegisters = new Guid("223ae797-bd09-4f28-8241-2763bdc5f713");
        static public Guid guidFilterRegisters
        {
            get { return s_guidFilterRegisters; }
        }

        static private Guid s_guidFilterLocals = new Guid("b200f725-e725-4c53-b36a-1ec27aef12ef");
        static public Guid guidFilterLocals
        {
            get { return s_guidFilterLocals; }
        }

        static private Guid s_guidFilterAllLocals = new Guid("196db21f-5f22-45a9-b5a3-32cddb30db06");
        static public Guid guidFilterAllLocals
        {
            get { return s_guidFilterAllLocals; }
        }

        static private Guid s_guidFilterArgs = new Guid("804bccea-0475-4ae7-8a46-1862688ab863");
        static public Guid guidFilterArgs
        {
            get { return s_guidFilterArgs; }
        }

        static private Guid s_guidFilterLocalsPlusArgs = new Guid("e74721bb-10c0-40f5-807f-920d37f95419");
        static public Guid guidFilterLocalsPlusArgs
        {
            get { return s_guidFilterLocalsPlusArgs; }
        }

        static private Guid s_guidFilterAllLocalsPlusArgs = new Guid("939729a8-4cb0-4647-9831-7ff465240d5f");
        static public Guid guidFilterAllLocalsPlusArgs
        {
            get { return s_guidFilterAllLocalsPlusArgs; }
        }
    }
}
