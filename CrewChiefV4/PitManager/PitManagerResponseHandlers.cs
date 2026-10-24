using CrewChiefV4.Audio;

namespace CrewChiefV4.PitManager
{
    internal static class PitManagerResponseHandlers
    {
        /// <summary>
        /// The common response handlers for all games.  Some games may have
        /// their own special cases.
        /// </summary>
        private static readonly CrewChief crewChief = MainWindow.instance != null ? MainWindow.instance.crewChief : null;


        #region Public Methods

#pragma warning disable S3400 // Methods should not return constants
        public static bool PMrh_NoResponse()
#pragma warning restore S3400 // Methods should not return constants
        {
            return true;
        }
        public static bool PMrh_Acknowledge()
        {
            playMessage(AudioPlayer.folderAcknowlegeOK);
            return true;
        }

        public static bool PMrh_CantChangeThose()
        {
            playMessage("mandatory_pit_stops/cant_change_those");
            return true;
        }

        public static bool PMrh_CantDoThat()
        {
            playMessage("mandatory_pit_stops/cant_do_that");
            return true;
        }

        public static bool PMrh_ChangeAllTyres()
        {
            playMessage("mandatory_pit_stops/confirm_change_all_tyres");
            return true;
        }

        public static bool PMrh_ChangeFrontTyres()
        {
            playMessage("mandatory_pit_stops/confirm_change_front_tyres");
            return true;
        }

        public static bool PMrh_ChangeRearTyres()
        {
            playMessage("mandatory_pit_stops/confirm_change_rear_tyres");
            return true;
        }

        public static bool PMrh_ChangeLeftTyres()
        {
            playMessage("mandatory_pit_stops/confirm_change_left_side_tyres");
            return true;
        }

        public static bool PMrh_ChangeRightTyres()
        {
            playMessage("mandatory_pit_stops/confirm_change_right_side_tyres");
            return true;
        }

        public static bool PMrh_ChangeFrontLeftTyre()
        {
            playMessage("mandatory_pit_stops/confirm_change_front_left_only");
            return true;
        }

        public static bool PMrh_ChangeFrontRightTyre()
        {
            playMessage("mandatory_pit_stops/confirm_change_front_right_only");
            return true;
        }

        public static bool PMrh_ChangeRearLeftTyre()
        {
            playMessage("mandatory_pit_stops/confirm_change_rear_left_only");
            return true;
        }

        public static bool PMrh_ChangeRearRightTyre()
        {
            playMessage("mandatory_pit_stops/confirm_change_rear_right_only");
            return true;
        }

        public static bool PMrh_ChangeNoTyres()
        {
            playMessage("mandatory_pit_stops/confirm_change_no_tyres");
            return true;
        }

        public static bool PMrh_TyreCompoundDry()
        {
            playMessage("mandatory_pit_stops/confirm_dry_tyres");
            return true;
        }

        public static bool PMrh_TyreCompoundHard()
        {
            playMessage("mandatory_pit_stops/confirm_hard_tyres");
            return true;
        }

        public static bool PMrh_TyreCompoundMedium()
        {
            playMessage("mandatory_pit_stops/confirm_medium_tyres");
            return true;
        }

        public static bool PMrh_TyreCompoundSoft()
        {
            playMessage("mandatory_pit_stops/confirm_soft_tyres");
            return true;
        }

        public static bool PMrh_TyreCompoundSupersoft()
        {
            playMessage("mandatory_pit_stops/confirm_supersoft_tyres");
            return true;
        }

        public static bool PMrh_TyreCompoundUltrasoft()
        {
            playMessage("mandatory_pit_stops/confirm_ultrasoft_tyres");
            return true;
        }

        public static bool PMrh_TyreCompoundHypersoft()
        {
            playMessage("mandatory_pit_stops/confirm_hypersoft_tyres");
            return true;
        }

        public static bool PMrh_TyreCompoundIntermediate()
        {
            playMessage("mandatory_pit_stops/confirm_intermediate_tyres");
            return true;
        }

        public static bool PMrh_TyreCompoundWet()
        {
            playMessage("mandatory_pit_stops/confirm_wet_tyres");
            return true;
        }

        public static bool PMrh_TyreCompoundMonsoon()
        {
            playMessage("mandatory_pit_stops/confirm_monsoon_tyres");
            return true;
        }

        public static bool PMrh_TyreCompoundOption()
        {
            playMessage("mandatory_pit_stops/confirm_option_tyres");
            return true;
        }

        public static bool PMrh_TyreCompoundPrime()
        {
            playMessage("mandatory_pit_stops/confirm_prime_tyres");
            return true;
        }

        public static bool PMrh_TyreCompoundAlternate()
        {
            playMessage("mandatory_pit_stops/confirm_alternate_tyres");
            return true;
        }

        public static bool PMrh_TyreCompoundNext()
        {
            playMessage("mandatory_pit_stops/confirm_next_tyre_compound");
            return true;
        }

        public static bool PMrh_TyreCompoundNotAvailable()   // Warning response
        {
            playMessage("mandatory_pit_stops/confirm_requested_tyre_not_available");
            return true;
        }

        //public static bool PMrh_fuelToEnd()
        //{
        //    playMessage(AudioPlayer.folderFuelToEnd);
        //    return true;
        //}

        public static bool PMrh_FuelAddXlitres()
        {
            playMessage("mandatory_pit_stops/confirm_refuelling");
            return true;
        }

        public static bool PMrh_noFuel()
        {
            playMessage("mandatory_pit_stops/confirm_no_refuelling");
            return true;
        }

        public static bool PMrh_RepairAll()
        {
            playMessage("mandatory_pit_stops/confirm_fix_all");
            return true;
        }

        public static bool PMrh_RepairNone()
        {
            playMessage("mandatory_pit_stops/confirm_fix_nothing");
            return true;
        }

        public static bool PMrh_RepairBody()
        {
            playMessage("mandatory_pit_stops/confirm_fix_body");
            return true;
        }

        // These were needed then the PitManagerEventHandlersTable_*.tt files
        // were changed to null the response handler fields but they would be
        // needed again if PM takes over all pit handling. Comment them out
        // for now.

        //public static bool PMrh_RepairFrontAero()
        //{
        //    playMessage("mandatory_pit_stops/confirm_fix_front_aero");
        //    return true;
        //}

        //public static bool PMrh_RepairRearAero()
        //{
        //    playMessage("mandatory_pit_stops/confirm_fix_rear_aero");
        //    return true;
        //}

        //public static bool PMrh_RepairAeroNone()
        //{
        //    playMessage("mandatory_pit_stops/confirm_dont_fix_aero");
        //    return true;
        //}

        //public static bool PMrh_RepairSuspension()
        //{
        //    playMessage("mandatory_pit_stops/confirm_fix_suspension");
        //    return true;
        //}

        //public static bool PMrh_RepairSuspensionNone()
        //{
        //    playMessage("mandatory_pit_stops/confirm_dont_fix_suspension");
        //    return true;
        //}
        public static bool PMrh_ServePenalty()
        {
            playMessage("mandatory_pit_stops/will_be_serving_penalty");
            return true;
        }

        public static bool PMrh_DontServePenalty()
        {
            playMessage("mandatory_pit_stops/will_not_be_serving_penalty");
            return true;
        }

        // These are similar to the above but are not simple responses and
        // would need to be filled in

        //public static bool PMrh_HowManyIncidentPoints()
        //{
        //    Log.Fatal("TBD");
        //    return true;
        //}
        //public static bool PMrh_WhatsTheIncidentLimit()
        //{
        //    Log.Fatal("TBD");
        //    return true;
        //}

        //public static bool PMrh_WhatsMyIrating()
        //{
        //    Log.Fatal("TBD");
        //    return true;
        //}

        //public static bool PMrh_WhatsMyLicenseClass()
        //{
        //    Log.Fatal("TBD");
        //    return true;
        //}

        //public static bool PMrh_WhatsTheSof()
        //{
        //    Log.Fatal("TBD");
        //    return true;
        //}

        //public static bool PMrh_WhatsThePitActions()
        //{
        //    Log.Fatal("TBD");
        //    return true;
        //}

        #endregion Public Methods

        #region Private Methods

        // tyre compound responses
        private static void playMessage(string folder)
        {
            if (true) //SoundCache.hasSingleSound(folder))
            {
                if (crewChief != null) crewChief.audioPlayer.playMessageImmediately(new QueuedMessage(folder, 0));
            }
            else
            {
                crewChief.audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
            }
        }

        #endregion Private Methods
    }
}