using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrewChiefV4.Overlay
{
    // these are the common (hard-coded) chart subscriptions using only mapped data, applicable to all games
    public class CommonSubscriptions
    {
        private static OverlaySubscription[] commonSubscriptions = new OverlaySubscription[]
        {
            new OverlaySubscription("car speed", OverlayDataType.FLOAT, new string[] {"Speed"}, mappedDataFieldName: "PositionAndMotionData.CarSpeed", voiceCommandFragments: Configuration.getSpeechRecognitionPhrases("CHART_FRAGMENT_CAR_SPEED"), opponentDataFieldname: "Speed"),
            new OverlaySubscription("engine revs", OverlayDataType.FLOAT, new string[] {"RPM"}, mappedDataFieldName: "EngineData.EngineRpm", voiceCommandFragments: Configuration.getSpeechRecognitionPhrases("CHART_FRAGMENT_ENGINE_REVS")),

            new OverlaySubscription("gear", OverlayDataType.FLOAT, new string[] {"Gear"}, mappedDataFieldName: "TransmissionData.Gear", voiceCommandFragments:Configuration.getSpeechRecognitionPhrases("CHART_FRAGMENT_GEAR")),
            new OverlaySubscription("brake pressure", OverlayDataType.FLOAT, new string[] {"Brake"}, mappedDataFieldName: "ControlData.BrakePedal", voiceCommandFragments: Configuration.getSpeechRecognitionPhrases("CHART_FRAGMENT_BRAKE")),
            new OverlaySubscription("throttle position", OverlayDataType.FLOAT, new string[] {"Throttle"}, mappedDataFieldName: "ControlData.ThrottlePedal", voiceCommandFragments: Configuration.getSpeechRecognitionPhrases("CHART_FRAGMENT_THROTTLE")),
            new OverlaySubscription("pedal inputs", OverlayDataType.FLOAT, null, groupMemberIds: new string[]{"throttle position", "brake pressure", "gear"}, voiceCommandFragments: Configuration.getSpeechRecognitionPhrases("CHART_FRAGMENT_THROTTLE_GEAR_AND_BRAKE")),

            new OverlaySubscription("lf inner temp", OverlayDataType.FLOAT, new string[] {"left front inner tyre temp"}, mappedDataFieldName: "TyreData.FrontLeft_RightTemp", voiceCommandFragments: Configuration.getSpeechRecognitionPhrases("CHART_FRAGMENT_LF_INNER_TEMP")),
            new OverlaySubscription("lf middle temp", OverlayDataType.FLOAT, new string[] {"left front middle tyre temp"}, mappedDataFieldName: "TyreData.FrontLeft_CenterTemp", voiceCommandFragments: Configuration.getSpeechRecognitionPhrases("CHART_FRAGMENT_LF_MIDDLE_TEMP")),
            new OverlaySubscription("lf outer temp", OverlayDataType.FLOAT, new string[] {"left front outer tyre temp"}, mappedDataFieldName: "TyreData.FrontLeft_LeftTemp", voiceCommandFragments: Configuration.getSpeechRecognitionPhrases("CHART_FRAGMENT_LF_OUTER_TEMP")),
            new OverlaySubscription("rf inner temp", OverlayDataType.FLOAT, new string[] {"right front inner tyre temp"}, mappedDataFieldName: "TyreData.FrontRight_LeftTemp", voiceCommandFragments: Configuration.getSpeechRecognitionPhrases("CHART_FRAGMENT_RF_INNER_TEMP")),
            new OverlaySubscription("rf middle temp", OverlayDataType.FLOAT, new string[] {"right front middle tyre temp"}, mappedDataFieldName: "TyreData.FrontRight_CenterTemp", voiceCommandFragments: Configuration.getSpeechRecognitionPhrases("CHART_FRAGMENT_RF_MIDDLE_TEMP")),
            new OverlaySubscription("rf outer temp", OverlayDataType.FLOAT, new string[] {"right front outer tyre temp"}, mappedDataFieldName: "TyreData.FrontRight_RightTemp", voiceCommandFragments: Configuration.getSpeechRecognitionPhrases("CHART_FRAGMENT_RF_OUTER_TEMP")),

            new OverlaySubscription("lr inner temp", OverlayDataType.FLOAT, new string[] {"left rear inner tyre temp"}, mappedDataFieldName: "TyreData.RearLeft_RightTemp", voiceCommandFragments: Configuration.getSpeechRecognitionPhrases("CHART_FRAGMENT_LR_INNER_TEMP")),
            new OverlaySubscription("lr middle temp", OverlayDataType.FLOAT, new string[] {"left rear middle tyre temp"}, mappedDataFieldName: "TyreData.RearLeft_CenterTemp", voiceCommandFragments: Configuration.getSpeechRecognitionPhrases("CHART_FRAGMENT_LR_MIDDLE_TEMP")),
            new OverlaySubscription("lr outer temp", OverlayDataType.FLOAT, new string[] {"left rear outer tyre temp"}, mappedDataFieldName: "TyreData.RearLeft_LeftTemp", voiceCommandFragments: Configuration.getSpeechRecognitionPhrases("CHART_FRAGMENT_LR_OUTER_TEMP")),
            new OverlaySubscription("rr inner temp", OverlayDataType.FLOAT, new string[] {"right rear inner tyre temp"}, mappedDataFieldName: "TyreData.RearRight_LeftTemp", voiceCommandFragments: Configuration.getSpeechRecognitionPhrases("CHART_FRAGMENT_RR_INNER_TEMP")),
            new OverlaySubscription("rr middle temp", OverlayDataType.FLOAT, new string[] {"right rear middle tyre temp"}, mappedDataFieldName: "TyreData.RearRight_CenterTemp", voiceCommandFragments: Configuration.getSpeechRecognitionPhrases("CHART_FRAGMENT_RR_MIDDLE_TEMP")),
            new OverlaySubscription("rr outer temp", OverlayDataType.FLOAT, new string[] {"right rear outer tyre temp"}, mappedDataFieldName: "TyreData.RearRight_RightTemp", voiceCommandFragments: Configuration.getSpeechRecognitionPhrases("CHART_FRAGMENT_RR_OUTER_TEMP")),

            new OverlaySubscription("lf temps", OverlayDataType.FLOAT, null, groupMemberIds: new string[]{"lf inner temp", "lf middle temp", "lf outer temp"}, voiceCommandFragments: Configuration.getSpeechRecognitionPhrases("CHART_FRAGMENT_LF_TEMPS")),
            new OverlaySubscription("rf temps", OverlayDataType.FLOAT, null, groupMemberIds: new string[]{"rf inner temp", "rf middle temp", "rf outer temp"}, voiceCommandFragments: Configuration.getSpeechRecognitionPhrases("CHART_FRAGMENT_RF_TEMPS")),
            new OverlaySubscription("lr temps", OverlayDataType.FLOAT, null, groupMemberIds: new string[]{"lr inner temp", "lr middle temp", "lr outer temp"}, voiceCommandFragments: Configuration.getSpeechRecognitionPhrases("CHART_FRAGMENT_LR_TEMPS")),
            new OverlaySubscription("rr temps", OverlayDataType.FLOAT, null, groupMemberIds: new string[]{"rr inner temp", "rr middle temp", "rr outer temp"}, voiceCommandFragments: Configuration.getSpeechRecognitionPhrases("CHART_FRAGMENT_RR_TEMPS")),
        };

        public static List<OverlaySubscription> getApplicableCommonSubscriptions(List<OverlaySubscription> gameSpecificSubscriptions)
        {
            List<OverlaySubscription> applicableCommonSubscriptions = new List<OverlaySubscription>();
            int commonCount = 0;
            foreach (OverlaySubscription commonSubscription in CommonSubscriptions.commonSubscriptions)
            {
                bool add = true;
                foreach (OverlaySubscription gameSpecificSubscription in gameSpecificSubscriptions)
                {
                    if (gameSpecificSubscription.id == commonSubscription.id)
                    {
                        add = false;
                        break;
                    }
                    if (gameSpecificSubscription.isGroup && commonSubscription.isGroup)
                    {
                        if (Enumerable.SequenceEqual(gameSpecificSubscription.groupMemberIds.OrderBy(t => t), commonSubscription.groupMemberIds.OrderBy(t => t)))
                        {
                            add = false;
                            break;
                        }
                    }
                    else if (gameSpecificSubscription.fieldName == commonSubscription.fieldName)
                    {
                        add = false;
                        break;
                    }
                }
                if (add)
                {
                    commonCount++;
                    applicableCommonSubscriptions.Add(commonSubscription);
                }
            }
            Console.WriteLine("Added " + commonCount + " common subs to " + gameSpecificSubscriptions.Count + " game-specific subs");
            return applicableCommonSubscriptions;
        }
    }
}
