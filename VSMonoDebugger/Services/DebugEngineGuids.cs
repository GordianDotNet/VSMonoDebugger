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
    }
}
