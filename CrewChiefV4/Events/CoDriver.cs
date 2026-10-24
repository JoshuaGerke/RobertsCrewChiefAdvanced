/*
 * This monitor handles rally co-driver pacenotes.
 *
 * Official website: thecrewchief.org
 * License: MIT
 */
using CrewChiefV4.Audio;
using CrewChiefV4.GameState;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace CrewChiefV4.Events
{
    public class CoDriver : AbstractEvent
    {
        public enum CornerCallStyle
        {
            UNKNOWN,
            NUMBER_FIRST,
            DIRECTION_FIRST,
            DESCRIPTIVE,
            NUMBER_FIRST_REVERSED,
            DIRECTION_FIRST_REVERSED,
            MUTED,
        }

        // for making pace note corrections, and eventually stage recce
        public enum Direction
        {
            LEFT,
            RIGHT,
            UNKNOWN
        }

        // Acknowledgement: Based on IDs partially documented in WorkerBee's RBR Pacenotes plugin.
        // IDs above 10000 are fake and still need figuring out.
        //
        // NOTE: coner_hairpin* and variants is special, it is not coming from the game.  CoDriver pack
        //       has to map it if needed.
        [JsonConverter(typeof(StringEnumConverter))]
        public enum PacenoteType
        {
            // Weird naming is used to simplify sound reading.
            corner_1_left = 0,
            corner_square_left = 1,
            corner_3_left = 2,
            corner_4_left = 3,
            corner_5_left = 4,
            corner_6_left = 5,
            corner_6_right = 6,
            corner_5_right = 7,
            corner_4_right = 8,
            corner_3_right = 9,
            corner_square_right = 10,
            corner_1_right = 11,
            detail_twisty = 12,
            detail_distance_call = 13,
            detail_narrows = 14,
            detail_wideout = 15,
            detail_over_crest = 16,
            detail_ford = 17,
            detail_care = 18,
            detail_bump = 19,
            detail_jump = 20,
            detail_start = 21,
            detail_finish = 22,
            detail_split = 23,
            detail_end_of_track = 24,
            corner_flat_right = 25,
            corner_flat_left = 26,
            detail_bridge = 27,
            detail_go_straight = 28,
            detail_keep_right = 29,
            detail_keep_left = 30,
            detail_keep_middle = 31,
            detail_caution = 32,
            corner_2_left = 102,
            corner_2_right = 112,
            corner_left = 120,
            corner_right = 121,
            corner_right_into = 122,
            corner_left_into = 123,
            corner_right_left = 124,
            corner_left_right = 125,
            corner_right_around = 126,
            corner_left_around = 127,
            number_1 = 140,
            number_2 = 141,
            number_3 = 142,
            number_4 = 143,
            number_5 = 144,
            number_6 = 145,
            number_7 = 146,
            number_8 = 147,
            number_9 = 148,
            number_10 = 149,
            number_20 = 150,
            number_30 = 151,
            number_40 = 152,
            number_50 = 153,
            number_60 = 154,
            number_70 = 155,
            number_80 = 156,
            number_90 = 157,
            number_100 = 158,
            number_120 = 159,
            number_140 = 160,
            number_150 = 161,
            number_160 = 162,
            number_180 = 163,
            number_200 = 164,
            number_250 = 165,
            number_300 = 166,
            number_350 = 167,
            number_400 = 168,
            number_450 = 169,
            number_500 = 170,
            number_600 = 171,
            number_700 = 172,
            number_800 = 173,
            number_900 = 174,
            number_1000 = 175,
            detail_over_bridge = 200,
            detail_over_rails = 201,
            detail_over_railway = 202,
            detail_deep_cut = 211,
            detail_full_cut = 212,
            detail_keep_centre = 213,
            detail_full = 214,
            detail_go_full = 215,
            detail_flatout = 216,
            detail_brake = 217,
            detail_light_cut = 218,
            detail_handbrake = 219,
            detail_keep_in = 220,
            detail_keep_out = 221,
            detail_clip = 223,
            detail_take = 224,
            detail_from_left = 224,
            detail_from_right = 224,
            detail_minus = 230,
            detail_minusminus = 231,
            detail_plus = 232,
            detail_plus_plus = 233,
            detail_early = 234,
            detail_late = 235,
            detail_easy = 236,
            detail_much = 237,
            detail_many = 238,
            detail_very = 239,
            detail_hard = 240,
            detail_fast = 241,
            detail_slow = 242,
            detail_exact = 243,
            detail_slowing = 244,
            detail_directly = 245,
            detail_light = 246,
            detail_big = 247,
            detail_small = 248,
            detail_sharp = 249,
            detail_round = 250,
            detail_tight = 251,
            detail_slight = 252,
            detail_good = 253,
            detail_bad = 254,
            detail_narrow = 255,
            detail_wide = 256,
            detail_straight = 257,
            detail_extra = 258,
            detail_uphill = 260,
            detail_downhill = 261,
            detail_longlong = 263,
            detail_short = 264,
            detail_short_short = 265,
            detail_go_narrow = 266,
            detail_go_wide = 267,
            detail_slippery = 268,
            detail_slide = 269,
            detail_understeer = 270,
            detail_sideways = 271,
            detail_hook = 272,
            detail_draws_in = 273,
            detail_very_long = 274,
            detail_very_short = 275,
            detail_curbside = 276,
            detail_slippy = 277,
            detail_muddy = 290,
            detail_dirty = 291,
            detail_bumpy = 292,
            detail_cramped = 293,
            detail_positive = 294,
            detail_negative = 295,
            detail_dirt = 296,
            detail_opens = 298,
            detail_fakes = 299,
            detail_bumps = 300,
            detail_hidden = 301,
            detail_blind = 302,
            detail_double_caution = 303,
            detail_triple_caution = 304,
            detail_gravel = 320,
            detail_tarmac = 321,
            detail_concrete = 322,
            detail_cobbles = 323,
            detail_grit = 324,
            detail_snow = 325,
            detail_onsplit = 326,
            detail_icy = 327,
            detail_rubble = 328,
            detail_ice = 329,
            detail_loose_gravel = 330,
            detail_crest = 340,
            detail_hollow = 341,
            detail_camber = 342,
            detail_reverse_camber = 343,
            detail_hole = 344,
            detail_ruts = 345,
            detail_deepruts = 346,
            detail_border = 347,
            detail_edge = 348,
            detail_curb = 349,
            detail_ditch = 350,
            detail_junction = 351,
            detail_curve = 352,
            detail_turn = 353,
            detail_steep_drop = 354,
            detail_bad_camber = 355,
            detail_shoulder = 356,
            detail_steep_hill = 357,
            detail_steep_incline = 358,
            detail_steep_slope = 359,
            detail_snow_border = 360,
            detail_dip = 361,
            detail_drops = 362,
            detail_drops_left = 363,
            detail_drops_right = 364,
            detail_fork_left = 365,
            detail_fork_right = 366,
            detail_negative_camber = 367,
            detail_positive_camber = 368,
            detail_compression = 369,
            detail_fence = 370,
            detail_wall = 371,
            detail_house = 372,
            detail_tree = 373,
            detail_stump = 374,
            detail_mast = 375,
            detail_post = 376,
            detail_island = 377,
            detail_chicane = 378,
            detail_stone = 379,
            detail_rock = 380,
            detail_tunnel = 381,
            detail_road = 382,
            detail_walk = 383,
            detail_rails = 384,
            detail_sign = 385,
            detail_bush = 386,
            detail_path = 387,
            detail_water = 388,
            detail_puddle = 389,
            detail_netting = 390,
            detail_tape = 391,
            detail_left_entry_chicane = 392,
            detail_right_entry_chicane = 393,
            detail_tyres = 394,
            detail_spectators = 395,
            detail_marshalls = 396,
            detail_barrels = 397,
            detail_roundabout = 399,
            detail_through = 400,
            detail_after = 401,
            detail_near = 402,
            detail_on = 403,
            detail_until = 404,
            detail_at = 405,
            detail_before = 406,
            detail_over = 407,
            detail_in = 408,
            detail_behind = 409,
            detail_for = 410,
            detail_inside = 411,
            detail_outside = 412,
            detail_then = 413,
            detail_off = 414,
            detail_from = 415,
            detail_in_de = 416,
            detail_done = 430,
            detail_stop = 431,
            detail_line = 432,
            detail_lifts = 433,
            detail_wide_d_e = 434,
            detail_next_lap = 435,
            detail_take_exit = 436,
            detail_wooden_fence = 443,
            detail_onto_gravel = 540,
            detail_onto_tarmac = 541,
            detail_onto_concrete = 542,
            detail_onto_cobbles = 543,
            detail_onto_grit = 544,
            detail_onto_snow = 545,
            detail_wet = 546,
            detail_dry = 547,
            detail_damp = 548,
            detail_hold = 549,
            detail_take_speed = 550,
            detail_left_foot_braking = 551,
            detail_grip_off = 552,
            detail_grip = 553,
            detail_good_grip = 554,
            detail_to_sight_distance = 555,
            detail_split_time = 556,
            detail_entry = 557,
            detail_checkpoint = 558,
            detail_speed = 559,
            detail_tightens_to_6 = 2001,
            detail_tightens_to_5 = 2002,
            detail_tightens_to_4 = 2003,
            detail_tightens_to_3 = 2004,
            detail_tightens_to_2 = 2005,
            detail_tightens_to_1 = 2006,
            detail_tightens_to_hairpin = 2007,
            detail_tightens_to_acute = 2027,
            detail_tightens_to_6_plus = 2062,
            detail_tightens_to_5_plus = 2063,
            detail_tightens_to_4_plus = 2064,
            detail_tightens_to_3_plus = 2065,
            detail_tightens_to_2_plus = 2264,
            detail_tightens_to_1_plus = 2066,
            detail_tightens_to_open_hairpin = 2067,
            detail_to_6 = 2008,
            detail_to_5 = 2009,
            detail_to_4 = 2010,
            detail_to_3 = 2011,
            detail_to_2 = 2012,
            detail_to_1 = 2013,
            detail_to_acute = 2014,
            detail_tightens_late = 2015,
            detail_dont_cut_early = 2016,
            detail_dont_cut_late = 2017,
            detail_opens_tightens = 2018,
            detail_tightens_opens = 2019,
            detail_stay_out = 2020,
            detail_care_in = 2021,
            detail_care_out = 2022,
            detail_late_apex = 2023,
            detail_to_dip = 2024,
            detail_continues_over_crest = 2029,
            detail_immediate = 2030,
            corner_left_acute = 2040,
            corner_right_acute = 2041,
            corner_6_right_plus = 2050,
            corner_5_right_plus = 2051,
            corner_4_right_plus = 2052,
            corner_3_right_plus = 2053,
            corner_2_right_plus = 2262,
            corner_1_right_plus = 2054,
            corner_open_hairpin_right_rbr = 2055,
            corner_6_left_plus = 2056,
            corner_5_left_plus = 2057,
            corner_4_left_plus = 2058,
            corner_3_left_plus = 2059,
            corner_2_left_plus = 2263,
            corner_1_left_plus = 2060,
            corner_open_hairpin_left_rbr = 2061,
            detail_keep_left_rbr = 2100,
            detail_keep_right_rbr = 2101,
            detail_double = 2102,
            detail_half_long = 2103,
            detail_to_finish = 2105,
            detail_jump_flat = 2106,
            detail_jump_bind = 2107,
            detail_over_jump = 2108,
            detail_small_crest = 2109,

            // Janne V3 mod details
            detail_detail_niu_jatkuu = 2025,
            detail_flat_scale = 2026,
            detail_toacute = 2028,
            detail_nips = 2031,
            detail_openslittle = 2032,
            detail_openslong = 2033,
            detail_opensverylong = 2034,
            detail_opensearly = 2035,
            detail_opensatjunction = 2036,
            detail_opensovercrest = 2037,
            detail_scaleplus = 2038,
            detail_scaleminus = 2039,
            detail_tightensearly = 2042,
            detail_tightenslittle = 2043,
            detail_tightenslong = 2044,
            detail_tightensverylong = 2045,
            detail_tightensatjunction = 2046,
            detail_tightensovercrest = 2047,
            detail_neat = 2049,
            detail_toflatminus = 2068,
            detail_toquick = 2069,
            detail_toem = 2070,
            detail_toplus = 2071,
            detail_tominus = 2072,
            detail_toveryslow = 2073,
            detail_flatminus_scale = 2074,
            detail_easy_scale = 2075,
            detail_quick_scale = 2076,
            detail_fast_scale = 2077,
            detail_em_scale = 2078,
            detail_plus_scale = 2079,
            detail_k_scale = 2080,
            detail_square_scale = 2081,
            detail_minus_scale = 2082,
            detail_slow_scale = 2083,
            detail_veryslow_scale = 2084,
            detail_remember = 2085,
            detail_patient = 2086,
            detail_small_jump = 2087,
            detail_over_big_jump = 2088,
            detail_continuesoversmallcrest = 2089,
            detail_longcrest = 2090,
            detail_rut = 2091,
            detail_to_bump = 2092,
            detail_twisty_fromleft = 2093,
            detail_twisty_fromright = 2094,
            detail_dontblock = 2095,
            detail_grip_middle = 2096,
            detail_grip_in = 2097,
            detail_grip_out = 2098,
            detail_go_round = 2099,
            detail_niu_loiva_alku = 2104,
            detail_bale_in = 2135,
            detail_bale_out = 2136,
            detail_tires_in = 2137,
            detail_tires_out = 2138,
            detail_rock_in = 2139,
            detail_rock_out = 2140,
            detail_fence_in = 2141,
            detail_fence_out = 2142,
            detail_tree_in = 2143,
            detail_tree_out = 2144,
            detail_ditch_in = 2145,
            detail_ditch_out = 2146,
            detail_drop_in = 2147,
            detail_drop_out = 2148,
            detail_house_in = 2149,
            detail_house_out = 2150,
            detail_niu_barrier_in = 2151,
            detail_niu_barrier_out = 2152,
            detail_sign_in = 2153,
            detail_sign_out = 2154,
            detail_post_in = 2155,
            detail_post_out = 2156,
            detail_log_in = 2157,
            detail_log_out = 2158,
            detail_cut_through = 2159,
            detail_at_tree = 2160,
            detail_at_trees = 2161,
            detail_at_bale = 2162,
            detail_at_rock = 2163,
            detail_at_guardrail = 2164,
            detail_at_banner = 2165,
            detail_at_tape = 2166,
            detail_at_fence = 2167,
            detail_at_flag = 2168,
            detail_at_house = 2169,
            detail_at_sign = 2170,
            detail_at_post = 2171,
            detail_at_junction = 2172,
            detail_at_crest = 2173,
            detail_at_gate = 2174,
            detail_at_bridge = 2175,
            detail_brake_into = 2176,
            detail_brake_and = 2177,
            detail_brake_20 = 2178,
            detail_brake_30 = 2179,
            detail_brake_40 = 2180,
            detail_brake_50 = 2181,
            detail_slippy_braking = 2182,
            detail_slippy_entry = 2183,
            detail_slippy_exit = 2184,
            detail_sumppu = 2185,
            detail_tightenssumppu = 2187,
            detail_jump_maybe = 2188,
            detail_over_small_jump = 2189,
            detail_over_jump_dab = 2190,
            detail_oversmallcrest = 2191,
            detail_overlongcrest = 2192,
            detail_overtwocrests = 2193,
            detail_overthreecrests = 2194,
            detail_doublecrest = 2195,
            detail_double_jump = 2196,
            detail_onto_straight = 2197,
            detail_at_exit = 2198,
            detail_at_opening = 2199,
            detail_logs_in = 2200,
            detail_logs_out = 2201,
            detail_short_left = 2202,
            detail_short_right = 2203,
            detail_slippery_maybe = 2204,
            detail_donut_right = 2205,
            detail_donut_left = 2206,
            detail_hp_scale = 2207,
            detail_acute_scale = 2208,
            detail_past_junction = 2209,
            detail_onto_bridge = 2210,
            detail_narrow_bridge = 2211,
            detail_onto_narrow_bridge = 2212,
            detail_narrow_gate = 2214,
            detail_through_narrow_gate = 2215,
            detail_extraextralong = 2216,
            detail_very_slippy = 2217,
            detail_at_bush = 2218,
            detail_big_sumppu = 2219,
            detail_small_sumppu = 2220,
            detail_onto_narrower_road = 2221,
            detail_onto_wider_road = 2222,
            detail_lift = 2223,
            detail_again = 2224,
            detail_to_crest = 2225,
            detail_left_over = 2226,
            detail_right_over = 2227,
            detail_precise = 2228,
            detail_stay_out_rbr = 2229,
            detail_bumpy_braking = 2230,
            detail_over_narrow_bridge = 2231,
            detail_gets_rougher = 2232,
            detail_gets_smoother = 2233,
            detail_gets_faster = 2234,
            detail_gets_slower = 2235,
            detail_surface_change = 2236,
            detail_bumps_over = 2237,
            detail_over_rocks = 2238,
            detail_onto_field = 2239,
            detail_fickle = 2240,
            detail_to_jump = 2241,
            detail_double_dont = 2242,
            detail_triple_dont = 2243,
            detail_important = 2244,
            detail_crest_to_left = 2245,
            detail_crest_to_right = 2246,
            detail_medium_scale = 2247,
            detail_fuckinglong = 2248,
            detail_tightensveryverylong = 2249,
            detail_tightensfuckinglong = 2250,
            detail_opensveryverylong = 2251,
            detail_opensfuckinglong = 2252,
            detail_tightenssharp = 2253,
            detail_tightensround = 2254,
            detail_openssharp = 2255,
            detail_opensround = 2256,
            detail_over_early_crest = 2257,
            detail_over_late_crest = 2258,
            detail_brake_10 = 2259,
            detail_black_ice = 2260,
            detail_at_log = 2261,
            detail_tosquare_plus = 2265,
            detail_post_early = 2266,
            detail_post_late = 2267,
            detail_posts = 2268,
            detail_overcrest_nolink = 2269,
            detail_care_nolink = 2270,
            detail_caution_nolink = 2271,
            detail_square_plus_scale = 2272,
            detail_late_cut = 2273,
            detail_at_logs = 2274,
            detail_niu_brake_jatkuu = 2275,
            detail_deep = 2276,
            detail_trust = 2277,
            detail_leftish = 2405,
            detail_rightish = 2406,
            detail_niu_small_crest_fabz = 2501,
            detail_niu_brow = 2503,
            detail_two_bales = 2507,
            detail_three_bales = 2508,
            detail_four_bales = 2509,
            detail_logs = 2512,
            detail_gate = 2515,
            detail_bales = 2521,
            detail_surface_note = 2571,
            detail_be_brave = 2600,
            detail_niu_watch = 2604,
            detail_bale = 2611,
            detail_niu_dab = 2615,
            detail_two_parts = 2622,
            detail_three_parts = 2623,
            detail_four_parts = 2624,
            detail_five_parts = 2625,
            detail_five_bales = 2626,

            // RBR-only special notes
            detail_empty_call = 4075,
            detail_caution_water = 4077,
            detail_onto = 4082,
            detail_into = 4083,
            detail_and = 4084,
            detail_thightens = 4088,
            detail_double_tightens = 4089,
            detail_long = 4092,

            // _don't care?
            detail_place_holder = 10005,
            detail_callout_time = 10006,
            detail_callout_distance = 10007,
            detail_sound_file = 10008,
            detail_standard_call = 10009,
            detail_sound_index = 10010,
            detail_callout_adjust = 10012,
            unknown = 20000,

            // CC-only
            detail_through_gate = 40001,
            detail_big_jump = 40002,
            corner_open_hairpin_left = 40003,
            corner_open_hairpin_right = 40004,
            detail_widens = 40005,
            detail_logs_inside = 40006,
            detail_rocks_inside = 40007,
            detail_tree_inside = 40008,
            detail_logs_outside = 40009,
            detail_rocks_outside = 40010,
            detail_tree_outside = 40011
        }

        [Flags]
        [JsonConverter(typeof(StringEnumConverter))]
        public enum PacenoteModifier : int
        {
            none = 0,
            detail_narrows = 1,
            detail_wideout = 2,
            detail_tightens = 4,
            detail_dont_cut = 32,
            detail_cut = 64,
            detail_double_tightens = 128,
            detail_opens = 256,
            detail_longlong = 512,
            detail_long = 1024,
            detail_minus = 2048,
            detail_plus = 4096,
            detail_maybe = 8192,
            detail_widens = 16384
        }

        private static readonly int[] distanceCallRanges = new int[]
        {
            30,
            40,
            50,
            60,
            70,
            80,
            100,
            120,
            140,
            150,
            160,
            180,
            200,
            250,
            300,
            350,
            400,
            450,
            500,
            600,
            700,
            800,
            900,
            1000
        };

        // Used for mapping corners to their reverse if cornerCallStyle is set to _REVERSED
        private static readonly Dictionary<string, string> reverseCorners = new Dictionary<string, string>
        {
            {"corner_1_left", "corner_6_left"},
            {"corner_2_left", "corner_5_left"},
            {"corner_3_left", "corner_4_left"},
            {"corner_4_left", "corner_3_left"},
            {"corner_5_left", "corner_2_left"},
            {"corner_6_left", "corner_1_left"},
            {"corner_1_right", "corner_6_right"},
            {"corner_2_right", "corner_5_right"},
            {"corner_3_right", "corner_4_right"},
            {"corner_4_right", "corner_3_right"},
            {"corner_5_right", "corner_2_right"},
            {"corner_6_right", "corner_1_right"},
            {"corner_1_left_plus", "corner_6_left_plus"},
            {"corner_2_left_plus", "corner_5_left_plus"},
            {"corner_3_left_plus", "corner_4_left_plus"},
            {"corner_4_left_plus", "corner_3_left_plus"},
            {"corner_5_left_plus", "corner_2_left_plus"},
            {"corner_6_left_plus", "corner_1_left_plus"},
            {"corner_1_right_plus", "corner_6_right_plus"},
            {"corner_2_right_plus", "corner_5_right_plus"},
            {"corner_3_right_plus", "corner_4_right_plus"},
            {"corner_4_right_plus", "corner_3_right_plus"},
            {"corner_5_right_plus", "corner_2_right_plus"},
            {"corner_6_right_plus", "corner_1_right_plus"}
        };

        // It turns out that chainedNotes not only define "into" logic, they also affect distance calculation.
        // That means we may need to allow specifying those in the .json.
#if false
        public static HashSet<CoDriver.PacenoteType> chainedNotes = new HashSet<CoDriver.PacenoteType>()
        {
            CoDriver.PacenoteType.corner_1_left,
            CoDriver.PacenoteType.corner_square_left,
            CoDriver.PacenoteType.corner_3_left,
            CoDriver.PacenoteType.corner_4_left,
            CoDriver.PacenoteType.corner_5_left,
            CoDriver.PacenoteType.corner_6_left,
            CoDriver.PacenoteType.corner_6_right,
            CoDriver.PacenoteType.corner_5_right,
            CoDriver.PacenoteType.corner_4_right,
            CoDriver.PacenoteType.corner_3_right,
            CoDriver.PacenoteType.corner_square_right,
            CoDriver.PacenoteType.corner_1_right,
            CoDriver.PacenoteType.corner_flat_right,
            CoDriver.PacenoteType.corner_flat_left,
            CoDriver.PacenoteType.corner_2_left,
            CoDriver.PacenoteType.corner_2_right,
            CoDriver.PacenoteType.corner_left,
            CoDriver.PacenoteType.corner_right,
            CoDriver.PacenoteType.corner_right_into,
            CoDriver.PacenoteType.corner_left_into,
            CoDriver.PacenoteType.corner_right_left,
            CoDriver.PacenoteType.corner_left_right,
            CoDriver.PacenoteType.corner_right_around,
            CoDriver.PacenoteType.corner_left_around,
            CoDriver.PacenoteType.detail_care,
            CoDriver.PacenoteType.detail_caution,
            CoDriver.PacenoteType.detail_double_caution,
            CoDriver.PacenoteType.detail_triple_caution,
            CoDriver.PacenoteType.detail_caution_water,
            CoDriver.PacenoteType.detail_hole,
            CoDriver.PacenoteType.detail_ruts,
            CoDriver.PacenoteType.detail_deepruts,
            CoDriver.PacenoteType.detail_post,
            CoDriver.PacenoteType.detail_mast,
            CoDriver.PacenoteType.detail_wall,
            CoDriver.PacenoteType.detail_fence,
            CoDriver.PacenoteType.detail_house,
            CoDriver.PacenoteType.detail_island,
            CoDriver.PacenoteType.detail_chicane,
            CoDriver.PacenoteType.detail_tunnel,
            CoDriver.PacenoteType.detail_rails,
            CoDriver.PacenoteType.detail_path,
            CoDriver.PacenoteType.detail_road,
            CoDriver.PacenoteType.detail_walk,
            CoDriver.PacenoteType.detail_roundabout,
            CoDriver.PacenoteType.detail_wooden_fence,
            CoDriver.PacenoteType.detail_twisty,
            CoDriver.PacenoteType.detail_go_straight,
            CoDriver.PacenoteType.detail_jump,
            /*CoDriver.PacenoteType.detail_over_crest,*/
            CoDriver.PacenoteType.detail_crest,
            CoDriver.PacenoteType.detail_bridge,
            CoDriver.PacenoteType.detail_ford,
            CoDriver.PacenoteType.detail_bump,
            CoDriver.PacenoteType.detail_water,
            CoDriver.PacenoteType.detail_puddle,
            CoDriver.PacenoteType.detail_tree,
            CoDriver.PacenoteType.detail_stump,
            CoDriver.PacenoteType.detail_stone,
            CoDriver.PacenoteType.detail_rock,
            CoDriver.PacenoteType.detail_sign,
            CoDriver.PacenoteType.detail_bush,
            CoDriver.PacenoteType.detail_barrels,
            /*CoDriver.PacenoteType.detail_over_bridge,
            CoDriver.PacenoteType.detail_over_rails,
            CoDriver.PacenoteType.detail_over_railway,*/
            CoDriver.PacenoteType.detail_netting,
            CoDriver.PacenoteType.detail_tyres,
            CoDriver.PacenoteType.detail_spectators,
            CoDriver.PacenoteType.detail_marshalls,
            CoDriver.PacenoteType.detail_tape,
            CoDriver.PacenoteType.detail_junction,
            CoDriver.PacenoteType.detail_curve,
            CoDriver.PacenoteType.detail_turn,
            CoDriver.PacenoteType.detail_left_entry_chicane,
            CoDriver.PacenoteType.detail_right_entry_chicane,
            CoDriver.PacenoteType.detail_fork_left,
            CoDriver.PacenoteType.detail_fork_right
        };
#endif

        // For chained notes, we can drop the 'into' and use 'over...' instead for these.
        private Dictionary<CoDriver.PacenoteType, CoDriver.PacenoteType> intoToOver = new Dictionary<CoDriver.PacenoteType, CoDriver.PacenoteType>()
        {
            { CoDriver.PacenoteType.detail_rails, CoDriver.PacenoteType.detail_over_rails },
            { CoDriver.PacenoteType.detail_jump, CoDriver.PacenoteType.detail_over_jump },
            { CoDriver.PacenoteType.detail_crest, CoDriver.PacenoteType.detail_over_crest },
            { CoDriver.PacenoteType.detail_bridge, CoDriver.PacenoteType.detail_over_bridge }
        };

        private Dictionary<string, PacenoteType> possibleTightensCommands = new Dictionary<string, PacenoteType>();

        // this needs to be an ordered dictionary so we can look for more specific corner names (open hairpin) before less specific names
        // (hairpin) - as we iterate we expect to encounter more specific names first
        private OrderedDictionary possibleCornerCommands = new OrderedDictionary();

        private Dictionary<string[], PacenoteType> obstaclePacenoteTypes = new Dictionary<string[], PacenoteType>()
        {
            { SpeechRecogniser.RALLY_BAD_CAMBER, PacenoteType.detail_bad_camber },
            { SpeechRecogniser.RALLY_OVER_BRIDGE, PacenoteType.detail_over_bridge },
            { SpeechRecogniser.RALLY_BRIDGE, PacenoteType.detail_bridge },
            { SpeechRecogniser.RALLY_BUMPS, PacenoteType.detail_bumps },
            { SpeechRecogniser.RALLY_CONCRETE, PacenoteType.detail_concrete },
            { SpeechRecogniser.RALLY_OVER_CREST, PacenoteType.detail_over_crest },
            { SpeechRecogniser.RALLY_CREST, PacenoteType.detail_crest },
            { SpeechRecogniser.RALLY_FORD, PacenoteType.detail_ford },
            { SpeechRecogniser.RALLY_LOOSE_GRAVEL, PacenoteType.detail_loose_gravel },
            { SpeechRecogniser.RALLY_GRAVEL, PacenoteType.detail_gravel },
            { SpeechRecogniser.RALLY_SNOW, PacenoteType.detail_snow },
            { SpeechRecogniser.RALLY_SLIPPY, PacenoteType.detail_slippy},
            { SpeechRecogniser.RALLY_OVER_JUMP, PacenoteType.detail_over_jump },
            { SpeechRecogniser.RALLY_BIG_JUMP, PacenoteType.detail_big_jump },
            { SpeechRecogniser.RALLY_JUMP, PacenoteType.detail_jump },
            { SpeechRecogniser.RALLY_JUNCTION, PacenoteType.detail_junction },
            { SpeechRecogniser.RALLY_KEEP_IN, PacenoteType.detail_keep_in },
            { SpeechRecogniser.RALLY_KEEP_LEFT, PacenoteType.detail_keep_left },
            { SpeechRecogniser.RALLY_KEEP_MIDDLE, PacenoteType.detail_keep_middle },
            { SpeechRecogniser.RALLY_KEEP_OUT, PacenoteType.detail_keep_out },
            { SpeechRecogniser.RALLY_KEEP_RIGHT, PacenoteType.detail_keep_right },
            { SpeechRecogniser.RALLY_LEFT_ENTRY_CHICANE, PacenoteType.detail_left_entry_chicane },
            { SpeechRecogniser.RALLY_OPENS_THEN_TIGHTENS, PacenoteType.detail_opens_tightens },
            { SpeechRecogniser.RALLY_TIGHTENS_THEN_OPENS, PacenoteType.detail_tightens_opens },
            { SpeechRecogniser.RALLY_OPENS, PacenoteType.detail_opens },
            { SpeechRecogniser.RALLY_WIDENS, PacenoteType.detail_widens },
            { SpeechRecogniser.RALLY_OVER_RAILS, PacenoteType.detail_over_rails },
            { SpeechRecogniser.RALLY_RIGHT_ENTRY_CHICANE, PacenoteType.detail_right_entry_chicane },
            { SpeechRecogniser.RALLY_DEEP_RUTS, PacenoteType.detail_deepruts },
            { SpeechRecogniser.RALLY_RUTS, PacenoteType.detail_ruts },
            { SpeechRecogniser.RALLY_TARMAC, PacenoteType.detail_onto_tarmac },
            { SpeechRecogniser.RALLY_TUNNEL, PacenoteType.detail_tunnel },
            { SpeechRecogniser.RALLY_CARE, PacenoteType.detail_care },
            { SpeechRecogniser.RALLY_CAUTION, PacenoteType.detail_caution },
            { SpeechRecogniser.RALLY_DOUBLE_CAUTION, PacenoteType.detail_double_caution },
            { SpeechRecogniser.RALLY_DANGER, PacenoteType.detail_triple_caution },
            { SpeechRecogniser.RALLY_THROUGH_GATE, PacenoteType.detail_through_gate },
            { SpeechRecogniser.RALLY_NARROWS, PacenoteType.detail_narrows },
            { SpeechRecogniser.RALLY_LOGS_INSIDE, PacenoteType.detail_logs_inside },
            { SpeechRecogniser.RALLY_ROCKS_INSIDE, PacenoteType.detail_rocks_inside },
            { SpeechRecogniser.RALLY_TREE_INSIDE, PacenoteType.detail_tree_inside },
            { SpeechRecogniser.RALLY_LOGS_OUTSIDE, PacenoteType.detail_logs_outside },
            { SpeechRecogniser.RALLY_ROCKS_OUTSIDE, PacenoteType.detail_rocks_outside },
            { SpeechRecogniser.RALLY_TREE_OUTSIDE, PacenoteType.detail_tree_outside },
            { SpeechRecogniser.RALLY_UPHILL, PacenoteType.detail_uphill },
            { SpeechRecogniser.RALLY_DOWNHILL, PacenoteType.detail_downhill },
            { SpeechRecogniser.RALLY_BRAKE, PacenoteType.detail_brake },
            { SpeechRecogniser.RALLY_GO_STRAIGHT, PacenoteType.detail_go_straight },
            { SpeechRecogniser.RALLY_TWISTY, PacenoteType.detail_twisty },
            { SpeechRecogniser.RALLY_DIP, PacenoteType.detail_dip},

            { SpeechRecogniser.RALLY_TIGHTENS_TO_1, PacenoteType.detail_tightens_to_1},
            { SpeechRecogniser.RALLY_TIGHTENS_TO_2, PacenoteType.detail_tightens_to_2},
            { SpeechRecogniser.RALLY_TIGHTENS_TO_3, PacenoteType.detail_tightens_to_3},
            { SpeechRecogniser.RALLY_TIGHTENS_TO_4, PacenoteType.detail_tightens_to_4},
            { SpeechRecogniser.RALLY_TIGHTENS_TO_5, PacenoteType.detail_tightens_to_5},
            { SpeechRecogniser.RALLY_TIGHTENS_TO_HAIRPIN, PacenoteType.detail_tightens_to_acute},

            { SpeechRecogniser.RALLY_INTO, PacenoteType.detail_into},
            { SpeechRecogniser.RALLY_THEN, PacenoteType.detail_then},
            { SpeechRecogniser.RALLY_AND, PacenoteType.detail_and}
        };

        public class Terminology
        {
            public Dictionary<string, string> terminology = new Dictionary<string, string>();
        }

        public class Terminologies
        {
            public Dictionary<string, CoDriver.Terminology> termininologies = new Dictionary<string, CoDriver.Terminology>();
            public HashSet<string> chainedNotes = new HashSet<string>();
        }

        // note this these historic calls contain the original call details (type, modifier, distance) NOT the corrected ones
        public class HistoricCall
        {
            public DateTime callTime = DateTime.MinValue;
            public float callDistance = 0;
            public PacenoteType callType = PacenoteType.unknown;
            public PacenoteModifier modifier = PacenoteModifier.none;

            public HistoricCall(CoDriverPacenote pacenote, DateTime callTime)
            {
                this.callType = pacenote.Pacenote;
                this.modifier = pacenote.Modifier;
                this.callDistance = pacenote.Distance;
                this.callTime = callTime;
            }
            public override string ToString()
            {
                return callType.ToString() + ":" + modifier.ToString();
            }
        }

        private const string pacenotesFileName = "pacenotes.json";
        private const string correctionsFileName = "corrections.json";

        public static Terminologies terminologies = new Terminologies();

        private const string codriverFolderPrefix = "codriver_";
        public const string defaultCodriverId = "Jim Britton (default)";
        public static List<String> availableCodrivers = new List<String>();
        private static readonly string selectedCodrvier = UserSettings.GetUserSettings().getString("codriver_name");
        public static CornerCallStyle cornerCallStyle = CornerCallStyle.UNKNOWN;

        private readonly float rushedSpeedKmH = UserSettings.GetUserSettings().getFloat("codriver_rushed_speed");   // default 140 km/h
        private readonly float chainedPacenoteThresholdMeters = UserSettings.GetUserSettings().getFloat("codriver_chained_pacenote_threshold_distance");  // default 30m
        private readonly float lookaheadSecondsFromConfig = UserSettings.GetUserSettings().getFloat("codriver_lookahead_seconds");  // default 4s
        private readonly float rushedLookaheadSeconds = UserSettings.GetUserSettings().getFloat("codriver_rushed_lookahead_seconds");  // default 2s
        private readonly bool dynamicLookahead = UserSettings.GetUserSettings().getBoolean("codriver_dynamic_lookahead");
        private readonly float minSpacingForAutoDistanceCall = UserSettings.GetUserSettings().getFloat("codriver_min_space_for_auto_distance_call");  // 40m
        private float earlierLaterStepSeconds = 0.5f;   // step used when moving calls forward or back

        private float lookaheadSecondsToUse;
        private const float maxLookaheadSeconds = 10f;
        private const float minLookaheadSeconds = 0.5f;

        private int lastProcessedPacenoteIdx = 0;
        private bool isLost = false;
        private float lastProcessedLapDist = -1.0f;

        private DateTime lastIntoSoundPlayed = DateTime.MinValue;

        // used when switching between descriptive and numeric corner calls
        private bool preferReversedNumbers = false;

        // Sound folders.
        private static string folderCodriverPrefix = "codriver/";
        private static string loadedCodriverPrefix = "";
        private static string startRecce = "acknowledge_start_recce";
        private static string endRecce = "acknowledge_end_recce";
        private static string correction = "correction";
        // if available, we use a codriver-specific version of these
        private string folderAcknowlegeOK = AudioPlayer.folderAcknowlegeOK;
        private string folderDidntUnderstand = AudioPlayer.folderDidntUnderstand;
        private string folderNo = AudioPlayer.folderNo;
        private string folderAcknowledgeStartRecce = folderCodriverPrefix + startRecce;
        private string folderAcknowledgeEndRecce = folderCodriverPrefix + endRecce;
        private string folderCorrection = folderCodriverPrefix + correction;

        // These are to be combined with the folderCodriverPrefix string.
        public static string folderFalseStart;
        public static string folderMicCheck;

        private DateTime lastRushedPacenoteTime = DateTime.MinValue;
        private int lastBatchFragmentCount = 0;

        private LinkedList<HistoricCall> historicCalls = new LinkedList<HistoricCall>();

        private List<CoDriverPacenote> correctionsForCurrentSession = new List<CoDriverPacenote>();

        private List<CoDriverPacenote> recePaceNotes = new List<CoDriverPacenote>();
        private bool inReceMode = false;
        private bool lastRecePacenoteWasDistance = false;

        // this is the set of pacenotes just added when in recce mode or just played when in normal mode.
        // It's used to locate the pace notes we need to remove and replace (recce mode) and provide a set
        // of pace notes to replay (normal mode when requesting that the chief repeats the last message)
        private List<CoDriverPacenote> lastPlayedOrAddedBatch = new List<CoDriverPacenote>();
        private DateTime lastPlayedBatchTime = DateTime.MinValue;

        private string lastStageName = "";

        // random rally helper function
        public static double GetClosestValueForDistanceCall(double distanceToNext)
        {
            var closestRange = 1000;
            var minDistance = Math.Abs(closestRange - distanceToNext);
            foreach (var r in CoDriver.distanceCallRanges)
            {
                var distToRange = Math.Abs(r - distanceToNext);
                if (distToRange < minDistance)
                {
                    minDistance = distToRange;
                    closestRange = r;
                }
            }
            return closestRange;
        }

        public CoDriver(AudioPlayer audioPlayer)
        {
            // reload the settings
            CoDriver.cornerCallStyle = (CornerCallStyle)UserSettings.GetUserSettings().getInt("codriver_style");
            this.rushedSpeedKmH = UserSettings.GetUserSettings().getFloat("codriver_rushed_speed");
            this.chainedPacenoteThresholdMeters = UserSettings.GetUserSettings().getFloat("codriver_chained_pacenote_threshold_distance");
            this.lookaheadSecondsFromConfig = UserSettings.GetUserSettings().getFloat("codriver_lookahead_seconds");
            this.lookaheadSecondsToUse = this.lookaheadSecondsFromConfig;   // this can be changed on the fly

            this.rushedLookaheadSeconds = UserSettings.GetUserSettings().getFloat("codriver_rushed_lookahead_seconds");

            this.audioPlayer = audioPlayer;
            assemblePossibleCornerCommands();
            assemblePossibleTighensCommands();

            if (GlobalBehaviourSettings.racingType != CrewChief.RacingType.Rally)
            {
                Debug.WriteLine("Co-driver is not initialized because app is not running in a rally racing mode");
                return;
            }

            if (CoDriver.availableCodrivers.Count == 0)
            {
                CoDriver.availableCodrivers.Add(defaultCodriverId);
                try
                {
                    var soundsDirectory = new DirectoryInfo(AudioPlayer.soundFilesPathNoChiefOverride + "/voice");
                    var directories = soundsDirectory.GetDirectories();
                    foreach (var dir in directories)
                    {
                        if (dir.Name.StartsWith(codriverFolderPrefix) && dir.Name.Length > codriverFolderPrefix.Length)
                            CoDriver.availableCodrivers.Add(dir.Name.Substring(codriverFolderPrefix.Length));
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine("No co-driver sound folders available");
                    return;
                }
            }

            var selectedCodriver = UserSettings.GetUserSettings().getString("codriver_name");
            if (!CoDriver.defaultCodriverId.Equals(selectedCodriver))
            {
                if (Directory.Exists(AudioPlayer.soundFilesPathNoChiefOverride + "/voice/codriver_" + selectedCodriver))
                {
                    Console.WriteLine("Using co-driver: " + selectedCodriver);
                    CoDriver.folderCodriverPrefix = "codriver_" + selectedCodriver + "/";
                    string codriverAcknowledgeOK = CoDriver.folderCodriverPrefix + "OK";
                    string codriverNo = CoDriver.folderCodriverPrefix + "no";
                    string codriverAcknowledgeDidntUnderstand = CoDriver.folderCodriverPrefix + "didnt_understand";
                    string codriverStartRecce = CoDriver.folderCodriverPrefix + startRecce;
                    string codriverEndRecce = CoDriver.folderCodriverPrefix + endRecce;
                    string codriverCorrection = CoDriver.folderCodriverPrefix + correction;
                    if (SoundCache.availableSounds.Contains(codriverAcknowledgeOK))
                    {
                        this.folderAcknowlegeOK = codriverAcknowledgeOK;
                    }
                    if (SoundCache.availableSounds.Contains(codriverAcknowledgeDidntUnderstand))
                    {
                        this.folderDidntUnderstand = codriverAcknowledgeDidntUnderstand;
                    }
                    if (SoundCache.availableSounds.Contains(codriverNo))
                    {
                        this.folderNo = codriverNo;
                    }
                    if (SoundCache.availableSounds.Contains(codriverStartRecce))
                    {
                        this.folderAcknowledgeStartRecce = codriverStartRecce;
                    }
                    if (SoundCache.availableSounds.Contains(codriverEndRecce))
                    {
                        this.folderAcknowledgeEndRecce = codriverEndRecce;
                    }
                    if (SoundCache.availableSounds.Contains(codriverCorrection))
                    {
                        this.folderCorrection = codriverCorrection;
                    }
                }
            }
            CoDriver.folderFalseStart = CoDriver.folderCodriverPrefix + "penalty_false_start";
            CoDriver.folderMicCheck = CoDriver.folderCodriverPrefix + "microphone_check";

            if (CoDriver.loadedCodriverPrefix != CoDriver.folderCodriverPrefix)
            {
                try
                {
                    var overridePath = DataFiles.terminologies;

                    if (File.Exists(overridePath))
                    {
                        Console.WriteLine($"Loading co-driver terminology from the user override file.  Current style selected: {CoDriver.cornerCallStyle}.");
                    }
                    else
                    {
                        Console.WriteLine($"Loading co-driver terminology.  Current style selected: {CoDriver.cornerCallStyle}.");
                        overridePath = AudioPlayer.soundFilesPathNoChiefOverride + @"\voice\" + folderCodriverPrefix.Substring(0, folderCodriverPrefix.Length - 1) + "\\terminologies.json";
                    }
                    CoDriver.terminologies = JsonFiles.ReadFile<Terminologies>(overridePath, JsonFiles.Comments.Strip);

                    CoDriver.loadedCodriverPrefix = CoDriver.folderCodriverPrefix;

                    // Validate styles.
                    foreach (var t in CoDriver.terminologies.termininologies.Keys)
                    {
                        if (!Enum.TryParse<CoDriver.CornerCallStyle>(t, out var enumValue))
                            Console.WriteLine($"Corrupted terminology.  Value {t} is not a valid CoDriver.CornerCallStyle value.");
                    }

                    // Validate terminology note IDs.
                    foreach (var t in CoDriver.terminologies.termininologies.Values)
                    {
                        foreach (var id in t.terminology.Keys)
                        {
                            if (!Enum.TryParse<CoDriver.PacenoteType>(id, out var enumValue))
                                Console.WriteLine($"Corrupted terminology mapping.  Value {id} is not a valid CoDriver.PacenoteType value.");
                        }
                    }

                    // Validate chained note IDs.
                    foreach (var id in terminologies.chainedNotes)
                    {
                        if (!Enum.TryParse<CoDriver.PacenoteType>(id, out var enumValue))
                            Console.WriteLine($"Corrupted terminology chained note.  Value {id} is not a valid CoDriver.PacenoteType value.");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to load terminologies for co-driver: {selectedCodriver}.  Terminology mappings, distance calling and 'into' chaining will not function correctly.  Exception: {e.Message}");
                }
            }

            // for debugging command parsing, set recce mode and squirt a string straight into the respond method
            // this.inReceMode = true;
            // this.lastPlayedOrAddedBatch.Add(new CoDriverPacenote { Pacenote = PacenoteType.corner_1_left, Distance = 100 });
            // CrewChief.currentGameState = new GameStateData(DateTime.Now.Ticks);
            // this.respond("correction left two earlier");

#if false
            var terminologies = new Terminologies();
            var term = new Terminology();
            term.terminology.Add(CoDriver.PacenoteType.corner_1_left.ToString(), "corner_hairpin_left");
            term.terminology.Add(CoDriver.PacenoteType.corner_1_right.ToString(), "corner_hairpin_right");
            term.terminology.Add(CoDriver.PacenoteType.corner_2_left.ToString(), "corner_1_left");
            term.terminology.Add(CoDriver.PacenoteType.corner_2_right.ToString(), "corner_1_right");
            term.terminology.Add(CoDriver.PacenoteType.corner_square_left.ToString(), "corner_2_left");
            term.terminology.Add(CoDriver.PacenoteType.corner_square_right.ToString(), "corner_2_right");
            terminologies.termininologies.Add(CornerCallStyle.NUMBER_FIRST.ToString(), term);

            term = new Terminology();
            term.terminology.Add(CoDriver.PacenoteType.corner_1_left.ToString(), "corner_hairpin_left");
            term.terminology.Add(CoDriver.PacenoteType.corner_1_right.ToString(), "corner_hairpin_right");
            term.terminology.Add(CoDriver.PacenoteType.corner_2_left.ToString(), "corner_1_left");
            term.terminology.Add(CoDriver.PacenoteType.corner_2_right.ToString(), "corner_1_right");
            term.terminology.Add(CoDriver.PacenoteType.corner_square_left.ToString(), "corner_2_left");
            term.terminology.Add(CoDriver.PacenoteType.corner_square_right.ToString(), "corner_2_right");
            terminologies.termininologies.Add(CornerCallStyle.DIRECTION_FIRST.ToString(), term);

            term = new Terminology();
            terminologies.termininologies.Add(CornerCallStyle.DESCRIPTIVE.ToString(), term);

            term = new Terminology();
            term.terminology.Add(CoDriver.PacenoteType.corner_1_left.ToString(), "corner_hairpin_left");
            term.terminology.Add(CoDriver.PacenoteType.corner_1_right.ToString(), "corner_hairpin_right");
            term.terminology.Add(CoDriver.PacenoteType.corner_2_left.ToString(), "corner_1_left");
            term.terminology.Add(CoDriver.PacenoteType.corner_2_right.ToString(), "corner_1_right");
            term.terminology.Add(CoDriver.PacenoteType.corner_square_left.ToString(), "corner_2_left");
            term.terminology.Add(CoDriver.PacenoteType.corner_square_right.ToString(), "corner_2_right");
            terminologies.termininologies.Add(CornerCallStyle.NUMBER_FIRST_REVERSED.ToString(), term);

            term = new Terminology();
            term.terminology.Add(CoDriver.PacenoteType.corner_1_left.ToString(), "corner_hairpin_left");
            term.terminology.Add(CoDriver.PacenoteType.corner_1_right.ToString(), "corner_hairpin_right");
            term.terminology.Add(CoDriver.PacenoteType.corner_2_left.ToString(), "corner_1_left");
            term.terminology.Add(CoDriver.PacenoteType.corner_2_right.ToString(), "corner_1_right");
            term.terminology.Add(CoDriver.PacenoteType.corner_square_left.ToString(), "corner_2_left");
            term.terminology.Add(CoDriver.PacenoteType.corner_square_right.ToString(), "corner_2_right");
            terminologies.termininologies.Add(CornerCallStyle.DIRECTION_FIRST_REVERSED.ToString(), term);

            foreach (var msg in chainedNotes)
            {
                terminologies.chainedNotes.Add(msg.ToString());
            }

            using (StreamWriter file = File.CreateText(System.IO.Path.Combine(@"c:\temp\", "term.json")))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Formatting = Newtonsoft.Json.Formatting.Indented;
                serializer.Serialize(file, terminologies);
            }
#endif
        }

        private void assemblePossibleCornerCommands()
        {
            // Note that this needs to be assembled with the most specific commands first so we find, for example,
            // "open hairpin" before "hairpin"
            foreach (string direction in SpeechRecogniser.RALLY_LEFT)
            {
                foreach (string cornerType in SpeechRecogniser.RALLY_1)
                {
                    possibleCornerCommands[cornerType + " " + direction] = PacenoteType.corner_1_left;
                    possibleCornerCommands[direction + " " + cornerType] = PacenoteType.corner_1_left;
                }
                foreach (string cornerType in SpeechRecogniser.RALLY_2)
                {
                    possibleCornerCommands[cornerType + " " + direction] = PacenoteType.corner_2_left;
                    possibleCornerCommands[direction + " " + cornerType] = PacenoteType.corner_2_left;
                }
                foreach (string cornerType in SpeechRecogniser.RALLY_3)
                {
                    possibleCornerCommands[cornerType + " " + direction] = PacenoteType.corner_3_left;
                    possibleCornerCommands[direction + " " + cornerType] = PacenoteType.corner_3_left;
                }
                foreach (string cornerType in SpeechRecogniser.RALLY_4)
                {
                    possibleCornerCommands[cornerType + " " + direction] = PacenoteType.corner_4_left;
                    possibleCornerCommands[direction + " " + cornerType] = PacenoteType.corner_4_left;
                }
                foreach (string cornerType in SpeechRecogniser.RALLY_5)
                {
                    possibleCornerCommands[cornerType + " " + direction] = PacenoteType.corner_5_left;
                    possibleCornerCommands[direction + " " + cornerType] = PacenoteType.corner_5_left;
                }
                foreach (string cornerType in SpeechRecogniser.RALLY_6)
                {
                    possibleCornerCommands[cornerType + " " + direction] = PacenoteType.corner_6_left;
                    possibleCornerCommands[direction + " " + cornerType] = PacenoteType.corner_6_left;
                }
                foreach (string cornerType in SpeechRecogniser.RALLY_SQUARE)
                {
                    possibleCornerCommands[cornerType + " " + direction] = PacenoteType.corner_square_left;
                    possibleCornerCommands[direction + " " + cornerType] = PacenoteType.corner_square_left;
                }
                foreach (string cornerType in SpeechRecogniser.RALLY_FLAT)
                {
                    possibleCornerCommands[cornerType + " " + direction] = PacenoteType.corner_flat_left;
                    possibleCornerCommands[direction + " " + cornerType] = PacenoteType.corner_flat_left;
                }
                foreach (string cornerType in SpeechRecogniser.RALLY_OPEN_HAIRPIN)
                {
                    possibleCornerCommands[cornerType + " " + direction] = PacenoteType.corner_open_hairpin_left;
                    possibleCornerCommands[direction + " " + cornerType] = PacenoteType.corner_open_hairpin_left;
                }
                foreach (string cornerType in SpeechRecogniser.RALLY_HAIRPIN)
                {
                    possibleCornerCommands[cornerType + " " + direction] = PacenoteType.corner_left_acute;
                    possibleCornerCommands[direction + " " + cornerType] = PacenoteType.corner_left_acute;
                }
            }

            foreach (string direction in SpeechRecogniser.RALLY_RIGHT)
            {
                foreach (string cornerType in SpeechRecogniser.RALLY_1)
                {
                    possibleCornerCommands[cornerType + " " + direction] = PacenoteType.corner_1_right;
                    possibleCornerCommands[direction + " " + cornerType] = PacenoteType.corner_1_right;
                }
                foreach (string cornerType in SpeechRecogniser.RALLY_2)
                {
                    possibleCornerCommands[cornerType + " " + direction] = PacenoteType.corner_2_right;
                    possibleCornerCommands[direction + " " + cornerType] = PacenoteType.corner_2_right;
                }
                foreach (string cornerType in SpeechRecogniser.RALLY_3)
                {
                    possibleCornerCommands[cornerType + " " + direction] = PacenoteType.corner_3_right;
                    possibleCornerCommands[direction + " " + cornerType] = PacenoteType.corner_3_right;
                }
                foreach (string cornerType in SpeechRecogniser.RALLY_4)
                {
                    possibleCornerCommands[cornerType + " " + direction] = PacenoteType.corner_4_right;
                    possibleCornerCommands[direction + " " + cornerType] = PacenoteType.corner_4_right;
                }
                foreach (string cornerType in SpeechRecogniser.RALLY_5)
                {
                    possibleCornerCommands[cornerType + " " + direction] = PacenoteType.corner_5_right;
                    possibleCornerCommands[direction + " " + cornerType] = PacenoteType.corner_5_right;
                }
                foreach (string cornerType in SpeechRecogniser.RALLY_6)
                {
                    possibleCornerCommands[cornerType + " " + direction] = PacenoteType.corner_6_right;
                    possibleCornerCommands[direction + " " + cornerType] = PacenoteType.corner_6_right;
                }
                foreach (string cornerType in SpeechRecogniser.RALLY_SQUARE)
                {
                    possibleCornerCommands[cornerType + " " + direction] = PacenoteType.corner_square_right;
                    possibleCornerCommands[direction + " " + cornerType] = PacenoteType.corner_square_right;
                }
                foreach (string cornerType in SpeechRecogniser.RALLY_FLAT)
                {
                    possibleCornerCommands[cornerType + " " + direction] = PacenoteType.corner_flat_right;
                    possibleCornerCommands[direction + " " + cornerType] = PacenoteType.corner_flat_right;
                }
                foreach (string cornerType in SpeechRecogniser.RALLY_OPEN_HAIRPIN)
                {
                    possibleCornerCommands[cornerType + " " + direction] = PacenoteType.corner_open_hairpin_right;
                    possibleCornerCommands[direction + " " + cornerType] = PacenoteType.corner_open_hairpin_right;
                }
                foreach (string cornerType in SpeechRecogniser.RALLY_HAIRPIN)
                {
                    possibleCornerCommands[cornerType + " " + direction] = PacenoteType.corner_right_acute;
                    possibleCornerCommands[direction + " " + cornerType] = PacenoteType.corner_right_acute;
                }
            }
        }

        private void assemblePossibleTighensCommands()
        {
            foreach (string tighens1 in SpeechRecogniser.RALLY_TIGHTENS_TO_1)
            {
                possibleTightensCommands[tighens1] = PacenoteType.detail_tightens_to_1;
            }
            foreach (string tighens2 in SpeechRecogniser.RALLY_TIGHTENS_TO_2)
            {
                possibleTightensCommands[tighens2] = PacenoteType.detail_tightens_to_2;
            }
            foreach (string tighens3 in SpeechRecogniser.RALLY_TIGHTENS_TO_3)
            {
                possibleTightensCommands[tighens3] = PacenoteType.detail_tightens_to_3;
            }
            foreach (string tighens4 in SpeechRecogniser.RALLY_TIGHTENS_TO_4)
            {
                possibleTightensCommands[tighens4] = PacenoteType.detail_tightens_to_4;
            }
            foreach (string tighens5 in SpeechRecogniser.RALLY_TIGHTENS_TO_5)
            {
                possibleTightensCommands[tighens5] = PacenoteType.detail_tightens_to_5;
            }
            foreach (string tighensHairpin in SpeechRecogniser.RALLY_TIGHTENS_TO_HAIRPIN)
            {
                possibleTightensCommands[tighensHairpin] = PacenoteType.detail_tightens_to_acute;
            }
        }

        public override List<CrewChief.RacingType> applicableRacingTypes
        {
            get { return new List<CrewChief.RacingType> { CrewChief.RacingType.Rally }; }
        }

        public override List<SessionType> applicableSessionTypes
        {
            get { return new List<SessionType> { SessionType.LonePractice, SessionType.HotLap, SessionType.Practice, SessionType.Qualify, SessionType.PrivateQualify, SessionType.Race }; }
        }

        public override List<SessionPhase> applicableSessionPhases
        {
            get { return new List<SessionPhase> { SessionPhase.Countdown, SessionPhase.Green, SessionPhase.Finished, SessionPhase.Checkered }; }
        }

        /*
         * IMPORTANT: This method is called twice - when the message becomes due, and immediately before playing it (which may have a
         * delay caused by the length of the queue at the time). So be *very* careful when checking and updating local state in here.
         */
        public override bool isMessageStillValid(string eventSubType, GameStateData currentGameState, Dictionary<String, Object> validationData)
        {
            if (base.isMessageStillValid(eventSubType, currentGameState, validationData))
                return true;

            return false;
        }

        public override void clearState()
        {
            this.lastProcessedPacenoteIdx = 0;
            this.isLost = false;
            this.lastProcessedLapDist = -1.0f;

            // Reset to config setting, as ToUse one can be adjusted via SRE.
            this.lookaheadSecondsToUse = this.lookaheadSecondsFromConfig;
            this.preferReversedNumbers = CoDriver.cornerCallStyle == CornerCallStyle.DIRECTION_FIRST_REVERSED || CoDriver.cornerCallStyle == CornerCallStyle.NUMBER_FIRST_REVERSED;
            this.lastRushedPacenoteTime = DateTime.MinValue;
            this.lastBatchFragmentCount = 0;
            this.historicCalls.Clear();
            this.recePaceNotes.Clear();
        }

        override protected void triggerInternal(GameStateData previousGameState, GameStateData currentGameState)
        {
            var pgs = previousGameState;

            if (pgs == null)
                return;

            var cgs = currentGameState;
            var csd = currentGameState.SessionData;
            var psd = previousGameState.SessionData;
            if (csd.TrackDefinition != null && csd.TrackDefinition.name != null && csd.TrackDefinition.name != "")
            {
                lastStageName = csd.TrackDefinition.name;
            }

            if (this.inReceMode)
            {
                if (csd.SessionPhase == SessionPhase.Green && psd.SessionPhase == SessionPhase.Countdown)
                {
                    Console.WriteLine("Stage recce started");
                }
            }
            else
            {
                if (CoDriver.cornerCallStyle != CornerCallStyle.MUTED)
                {
                    this.ProcessRaceStart(cgs, csd, psd);
                    if (Game.RBR)
                    {
                        this.ProcessPenalties(cgs, pgs);
                        this.ProcessLost(cgs, pgs);
                    }
                    this.ProcessPacenotes(cgs, csd);
                }
            }
        }

        private void ProcessLost(GameStateData cgs, GameStateData pgs)
        {
            if (cgs.SessionData.SessionPhase != SessionPhase.Green || cgs.PositionAndMotionData.DistanceRoundTrack == 0
                || this.isLost)
                return;

            if (this.lastProcessedLapDist == -1.0)
            {
                this.lastProcessedLapDist = cgs.PositionAndMotionData.DistanceRoundTrack;
                return;
            }

            // If distance suddenly jumps drastically, we're likely lost.
            if (Math.Abs(this.lastProcessedLapDist - cgs.PositionAndMotionData.DistanceRoundTrack) > 500.0f)
            {
                Console.WriteLine("CoDriver: we seem to be lost, turning pacenotes off.");

                this.audioPlayer.playMessageImmediately(new QueuedMessage(CoDriver.folderCodriverPrefix + "we_are_lost", 0));
                this.isLost = true;
                return;
            }

            this.lastProcessedLapDist = cgs.PositionAndMotionData.DistanceRoundTrack;
        }

        private void ProcessPenalties(GameStateData cgs, GameStateData pgs)
        {
            if (pgs.PenaltiesData.PenaltyCause == PenatiesData.DetailedPenaltyCause.NONE
                && cgs.PenaltiesData.PenaltyCause == PenatiesData.DetailedPenaltyCause.FALSE_START)
            {
                this.audioPlayer.playMessageImmediately(new QueuedMessage(CoDriver.folderCodriverPrefix + "penalty_false_start", 0));
                // this is only supported for RBR (no countdown data for Dirt). If we launch before zero we miss the transition from
                // countdown to green so the ProcessRaceStart call won't load the notes. So load them here
                LoadUserCreatedNotesAndCorrections(cgs);
            }
        }

        private void ProcessRaceStart(GameStateData cgs, SessionData csd, SessionData psd)
        {
            if (csd.SessionPhase == SessionPhase.Countdown)
            {
                if (psd.SessionRunningTime < -3.0f
                    && csd.SessionRunningTime > -3.0f)
                    this.audioPlayer.playMessageImmediately(new QueuedMessage(CoDriver.folderCodriverPrefix + PacenoteType.number_3, 0));

                if (psd.SessionRunningTime < -2.0f
                    && csd.SessionRunningTime > -2.0f)
                    this.audioPlayer.playMessageImmediately(new QueuedMessage(CoDriver.folderCodriverPrefix + PacenoteType.number_2, 0));

                if (psd.SessionRunningTime < -1.0f
                    && csd.SessionRunningTime > -1.0f)
                    this.audioPlayer.playMessageImmediately(new QueuedMessage(CoDriver.folderCodriverPrefix + PacenoteType.number_1, 0));
            }

            if (psd.SessionPhase == SessionPhase.Countdown
                && csd.SessionPhase == SessionPhase.Green
                && cgs.PenaltiesData.PenaltyCause == PenatiesData.DetailedPenaltyCause.NONE)
            {
                this.audioPlayer.playMessageImmediately(new QueuedMessage(CoDriver.folderCodriverPrefix + "detail_go", 0));
                LoadUserCreatedNotesAndCorrections(cgs);
            }
        }

        private void LoadUserCreatedNotesAndCorrections(GameStateData cgs)
        {
            // load saved pace notes
            LoadRecePaceNotes(cgs);
            // load the corrections here
            LoadAndApplyCorrections(cgs.SessionData.TrackDefinition.name, cgs.CoDriverPacenotes);
        }

        private void LoadAndApplyCorrections(string trackName, List<CoDriverPacenote> paceNotes)
        {
            // reset the corrections
            correctionsForCurrentSession = new List<CoDriverPacenote>();
            string pacenotesPath = GetPacenotesPath(trackName, true);
            if (Directory.Exists(pacenotesPath))
            {
                string correctionsFullFileName = Path.Combine(pacenotesPath, CoDriver.correctionsFileName);
                if (File.Exists(correctionsFullFileName))
                {
                    correctionsForCurrentSession = JsonFiles.ReadFile<List<CoDriverPacenote>>(correctionsFullFileName, JsonFiles.Comments.Strip);
                    if (correctionsForCurrentSession == null)
                    {
                        // empty file, ensure the local var is initialised
                        correctionsForCurrentSession = new List<CoDriverPacenote>();
                    }
                }
            }
            // apply the corrections
            foreach (CoDriverPacenote correction in correctionsForCurrentSession)
            {
                bool appliedAsCorrection = false;

                // special case for tightens corrections - we don't necessarily want to insert these at the end of a batch
                // so the standard logic for inserts isn't appropriate
                int pacenoteIndex = 0;
                CoDriverPacenote insertedCorrectionNote = null;
                foreach (CoDriverPacenote paceNote in paceNotes)
                {
                    // if the correction's original distance, type and modifier match the pace notes values, then this correction is for this pace note.
                    // There's an edge case here where 2 identical pace notes are in a batch (same distance, modifier and type). Not sure if that's worth addressing.
                    if (Math.Abs(correction.Distance - paceNote.Distance) < 1
                        && correction.Pacenote == paceNote.Pacenote
                        && correction.Modifier == paceNote.Modifier)
                    {
                        if (correction.AppendToNote != null && correction.AppendToNote.Value)
                        {
                            // special case, typically for adding a "tightens to..." correction, add this as a new note immediately after the one we're correcting
                            insertedCorrectionNote = new CoDriverPacenote
                            {
                                Pacenote = correction.GetPacenoteType(),
                                Modifier = correction.GetModifier(),
                                Distance = correction.GetDistance()
                            };
                        }
                        else
                        {
                            paceNote.CorrectedPacenoteType = correction.CorrectedPacenoteType;
                            paceNote.CorrectedPacenoteModifier = correction.CorrectedPacenoteModifier;
                            paceNote.CorrectedDistance = correction.CorrectedDistance;
                            Console.WriteLine("Pacenote " + paceNote.Pacenote + ": " + paceNote.Modifier + " corrected to " + correction.ToString());
                        }
                        appliedAsCorrection = true;
                        break;
                    }
                    pacenoteIndex++;
                }
                if (insertedCorrectionNote != null)
                {
                    paceNotes.Insert(pacenoteIndex + 1, insertedCorrectionNote);
                }
                if (!appliedAsCorrection)
                {
                    // look to insert this
                    for (int i = 0; i < paceNotes.Count; i++)
                    {
                        if (paceNotes[i].Pacenote == PacenoteType.detail_distance_call)
                        {
                            continue;
                        }
                        if (paceNotes[i].Distance > correction.Distance)
                        {
                            // we've found place to insert the new note, sanity check it first
                            if (correction.CorrectedPacenoteType != null && correction.CorrectedPacenoteModifier != null && correction.CorrectedDistance != null)
                            {
                                Console.WriteLine("Inserting pacenote " + correction.ToString());
                                paceNotes.Insert(i, new CoDriverPacenote
                                {
                                    Pacenote = correction.CorrectedPacenoteType.Value,
                                    Modifier = correction.CorrectedPacenoteModifier.Value,
                                    Distance = correction.CorrectedDistance.Value
                                });
                            }
                            break;
                        }
                    }
                }
            }
        }

        private void LoadRecePaceNotes(GameStateData cgs)
        {
            if (Game.DIRT || Game.DIRT_2)
            {
                // if we're playing Dirt rally, there are no game-provided pace notes. The session clear should have taken care of this
                // but there are cases where it doesn't that I've still not debugged
                cgs.CoDriverPacenotes.Clear();
                correctionsForCurrentSession.Clear();
                historicCalls.Clear();
            }
            string pacenotesPath = GetPacenotesPath(cgs.SessionData.TrackDefinition.name, true);
            if (Directory.Exists(pacenotesPath))
            {
                string pacenotesFullFileName = Path.Combine(pacenotesPath, CoDriver.pacenotesFileName);
                if (File.Exists(pacenotesFullFileName))
                {
                    List<CoDriverPacenote> paceNotes = JsonFiles.ReadFile<List<CoDriverPacenote>>(pacenotesFullFileName, JsonFiles.Comments.Strip);
                    if (paceNotes != null && paceNotes.Count > 0)
                    {
                        cgs.UseCrewchiefPaceNotes = true;
                        InsertDistanceData(paceNotes);
                        InsertFinish(paceNotes, cgs.SessionData.TrackDefinition.trackLength);
                        if (cgs.CoDriverPacenotes != null && cgs.CoDriverPacenotes.Count > 0)
                        {
                            Console.WriteLine("Replacing " + cgs.CoDriverPacenotes.Count + " game-provided stage notes with " + paceNotes.Count + " Crew Chief stage notes");
                        }
                        cgs.CoDriverPacenotes = paceNotes;
                    }
                }
            }
            this.lastProcessedPacenoteIdx = 0;
            this.isLost = false;
            this.lastProcessedLapDist = -1.0f;
        }

        private void InsertFinish(List<CoDriverPacenote> loadedPaceNotes, float trackLength)
        {
            int indexToInsertFinish = -1;
            for (int i = loadedPaceNotes.Count - 1; i >= 0; i--)
            {
                CoDriverPacenote paceNote = loadedPaceNotes[i];
                if (paceNote.GetPacenoteType() == PacenoteType.detail_finish || paceNote.GetPacenoteType() == PacenoteType.detail_to_finish)
                {
                    return;
                }
                else if (indexToInsertFinish != -1 && paceNote.GetDistance() <= trackLength)
                {
                    indexToInsertFinish = i + 1;
                    // allow the loop to continue here. We know where we want to insert the 'finish' pace note but continue iterating till we reach the start
                    // to ensure it's not in there already
                }
            }
            if (indexToInsertFinish > 0)
            {
                CoDriverPacenote finishPaceNote = new CoDriverPacenote();
                finishPaceNote.Distance = trackLength;
                finishPaceNote.Pacenote = PacenoteType.detail_to_finish;
                loadedPaceNotes.Insert(indexToInsertFinish, finishPaceNote);
            }
        }

        private void InsertDistanceData(List<CoDriverPacenote> loadedPaceNotes)
        {
            for (int i = 1; i < loadedPaceNotes.Count; i++)
            {
                if (loadedPaceNotes[i].Pacenote == PacenoteType.detail_distance_call && loadedPaceNotes[i].Options == null)
                {
                    // this is an autogenerated distance placeholder.
                    // Get the distance from the previous pace note to the next proper pacenote
                    int nextIndex = i + 1;
                    while (nextIndex < loadedPaceNotes.Count)
                    {
                        CoDriverPacenote nextPacenote = loadedPaceNotes[nextIndex];
                        CoDriver.PacenoteType nextPacenoteType = nextPacenote.GetPacenoteType();
                        if (nextPacenoteType != CoDriver.PacenoteType.detail_keep_centre
                            && nextPacenoteType != CoDriver.PacenoteType.detail_keep_left
                            && nextPacenoteType != CoDriver.PacenoteType.detail_keep_right
                            && nextPacenoteType != CoDriver.PacenoteType.detail_keep_left_rbr
                            && nextPacenoteType != CoDriver.PacenoteType.detail_keep_right_rbr
                            && nextPacenoteType != CoDriver.PacenoteType.detail_keep_middle
                            && nextPacenoteType != CoDriver.PacenoteType.detail_keep_out
                            && nextPacenoteType != CoDriver.PacenoteType.detail_keep_in
                            && nextPacenoteType != CoDriver.PacenoteType.detail_bumps
                            && nextPacenoteType != CoDriver.PacenoteType.detail_bump
                            && nextPacenoteType != CoDriver.PacenoteType.detail_ruts
                            && nextPacenoteType != CoDriver.PacenoteType.detail_deepruts
                            && nextPacenoteType != CoDriver.PacenoteType.detail_distance_call)
                        {
                            // next pace note is a real one so get the distance to call
                            float distanceToNext = loadedPaceNotes[i + 1].GetDistance() - loadedPaceNotes[i].GetDistance();
                            if (distanceToNext >= minSpacingForAutoDistanceCall)
                            {
                                loadedPaceNotes[i].Options = CoDriver.GetClosestValueForDistanceCall(distanceToNext);
                            }
                            break;
                        }
                        nextIndex++;
                    }
                }
            }
        }

        // gets a (very) rough estimate of how slow the car might need to be as it travels through a slow corner.
        // This is used only to adjust the read-ahead distance when we're expecting the car to be slowing significantly.
        // The numbers here aren't intended to be exact or anything, they're just intended to allow the pace notes
        // after the corner to be delayed.
        private float GetSlowestExpectedSpeedForBatch(List<CoDriverPacenote> batch, float currentSpeed)
        {
            float slowestSpeed = currentSpeed;
            if (batch != null)
            {
                foreach (CoDriverPacenote paceNote in batch)
                {
                    switch (paceNote.GetPacenoteType())
                    {
                        case PacenoteType.corner_1_left:
                        case PacenoteType.corner_1_right:
                        case PacenoteType.corner_left_acute:
                        case PacenoteType.corner_right_acute:
                            slowestSpeed = Math.Min(slowestSpeed, 15);
                            break;
                        case PacenoteType.corner_2_left:
                        case PacenoteType.corner_2_right:
                        case PacenoteType.corner_square_left:
                        case PacenoteType.corner_square_right:
                            slowestSpeed = Math.Min(slowestSpeed, 20);
                            break;
                        case PacenoteType.corner_3_left:
                        case PacenoteType.corner_3_right:
                        case PacenoteType.corner_open_hairpin_left:
                        case PacenoteType.corner_open_hairpin_right:
                            if (paceNote.GetModifier().HasFlag(PacenoteModifier.detail_tightens))
                            {
                                slowestSpeed = Math.Min(slowestSpeed, 25);
                            }
                            else if (paceNote.GetModifier().HasFlag(PacenoteModifier.detail_double_tightens))
                            {
                                slowestSpeed = Math.Min(slowestSpeed, 20);
                            }
                            else
                            {
                                slowestSpeed = Math.Min(slowestSpeed, 30);
                            }
                            break;
                        case PacenoteType.corner_4_left:
                        case PacenoteType.corner_4_right:
                            if (paceNote.GetModifier().HasFlag(PacenoteModifier.detail_tightens))
                            {
                                slowestSpeed = Math.Min(slowestSpeed, 30);
                            }
                            else if (paceNote.GetModifier().HasFlag(PacenoteModifier.detail_double_tightens))
                            {
                                slowestSpeed = Math.Min(slowestSpeed, 20);
                            }
                            break;
                        default:
                            if (paceNote.GetModifier().HasFlag(PacenoteModifier.detail_double_tightens))
                            {
                                slowestSpeed = Math.Min(slowestSpeed, 30);
                            }
                            break;
                    }
                }
            }
            return slowestSpeed;
        }

        private void ProcessPacenotes(GameStateData cgs, SessionData csd)
        {
            if (this.isLost)
                return;

            if (csd.SessionPhase == SessionPhase.Green
                && cgs.CoDriverPacenotes.Count > 0)
            {
                // NOTE: sometimes distance jumps significantly, that typically means we're lost on track.  Not sure we have to handle that though ("we're ***** lost" message?)

                // 4 secs of look ahead, by default.
                var currentSpeed = cgs.PositionAndMotionData.CarSpeed;
                // if our last batch is recent, use the expected slowest speed from it. Otherwise use our current speed.
                // readDist is recalculated on every iteration inside the loop
                float readDist;
                if (dynamicLookahead && cgs.Now < this.lastPlayedBatchTime.AddSeconds(3))
                {
                    // we've recently played a batch so use the expected speed from this batch
                    readDist = cgs.PositionAndMotionData.DistanceRoundTrack +
                        this.lookaheadSecondsToUse * GetSlowestExpectedSpeedForBatch(this.lastPlayedOrAddedBatch, currentSpeed);
                }
                else
                {
                    readDist = cgs.PositionAndMotionData.DistanceRoundTrack + this.lookaheadSecondsToUse * currentSpeed;
                }
                var nextBatchDistance = this.FindNextBatchDistance(readDist, cgs, out var fragmentsInCurrBatch);
#if DEBUG
                var reachedFinish = false;
#endif  // DEBUG

                List<CoDriverPacenote> pacenotesInBatch = new List<CoDriverPacenote>();

                // while looping, if we encounter a note with the same distance value as the one we just played, we always play
                // it regardless of the dynamic lookahead calculation. This applies to 'real' notes (not distance calls)
                float previousDistance = -1f;

                // play if we've reached the magic distance or this note is part of a batch recorded at the same distance as the previously played note in this iteration
                while (this.lastProcessedPacenoteIdx < cgs.CoDriverPacenotes.Count
                    && (readDist > cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].GetDistance()
                    || (previousDistance != -1 && previousDistance == cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].GetDistance())))
                {
                    if (this.ShouldIgnorePacenote(cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx]))
                    {
#if DEBUG
                        Console.WriteLine($"IGNORING PACENOTE: {cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].GetPacenoteType()}  at: {cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].GetDistance().ToString("0.000")}");
#endif  // DEBUG
                        ++this.lastProcessedPacenoteIdx;
                        continue;
                    }

                    // Finish is special: do not look ahead.
                    if (cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].GetPacenoteType() == CoDriver.PacenoteType.detail_finish
                        && cgs.PositionAndMotionData.DistanceRoundTrack < cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].GetDistance())
                    {
#if DEBUG
                        reachedFinish = true;
#endif  // DEBUG
                        break;
                    }

                    // Play the main pacenote.
                    var mainPacenoteDist = cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].GetDistance();
                    if (cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].GetPacenoteType() == CoDriver.PacenoteType.detail_distance_call)
                    {
                        // Distance call is not chained if "empty call" precedes it.
                        Console.WriteLine($"Playing pacenote: {cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].GetPacenoteType()}  at: {cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].GetDistance().ToString("0.000")}");
                        if (Enum.TryParse<CoDriver.PacenoteType>("number_" + cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].Options, out var pacenote))
                            this.audioPlayer.playMessageImmediately(new QueuedMessage(this.GetPacenoteMessageID(pacenote, mainPacenoteDist, nextBatchDistance, fragmentsInCurrBatch, currentSpeed, cgs.Now), 0));
#if DEBUG
                        else

                            Console.WriteLine($"DISTANCE PARSE FAILED: {cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].Options}  at: {cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].GetDistance().ToString("0.000")}");
#endif  // DEBUG
                    }
                    else
                    {
                        Console.WriteLine($"Playing pacenote: {cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].GetPacenoteType()}  " +
                            $"with pacenote distance: {cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].GetDistance().ToString("0.000")}  " +
                            $"at track distance {cgs.PositionAndMotionData.DistanceRoundTrack.ToString("0.000")}");
                        this.historicCalls.AddLast(new HistoricCall(cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx], cgs.Now));
                        this.audioPlayer.playMessageImmediately(new QueuedMessage(GetPacenoteMessageID(cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].GetPacenoteType(), mainPacenoteDist, nextBatchDistance, fragmentsInCurrBatch, currentSpeed, cgs.Now), 0));
                        pacenotesInBatch.Add(cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx]);
                    }

                    // Play modifiers.
                    playModifiers(cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].GetModifier(), cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].GetDistance(),
                        mainPacenoteDist, nextBatchDistance, fragmentsInCurrBatch, currentSpeed, cgs.Now);

                    var previousPacenoteType = cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].GetPacenoteType();
                    previousDistance = mainPacenoteDist;
                    ++this.lastProcessedPacenoteIdx;

                    // If next call is one of the chained calls, play them.  All this might be too RBR specific, but if there
                    // will ever be other rally games added, all this can be tweaked based on game definition.
                    while (this.lastProcessedPacenoteIdx < cgs.CoDriverPacenotes.Count)
                    {
                        if (cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].GetPacenoteType() == CoDriver.PacenoteType.detail_distance_call)
                        {
                            Console.WriteLine($"Playing chained pacenote: {cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].GetPacenoteType()}  at: {cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].GetDistance().ToString("0.000")}");
                            if (Enum.TryParse<CoDriver.PacenoteType>("number_" + cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].Options, out var pacenote))
                                this.audioPlayer.playMessageImmediately(new QueuedMessage(this.GetPacenoteMessageID(pacenote, mainPacenoteDist, nextBatchDistance, fragmentsInCurrBatch, currentSpeed, cgs.Now), 0));
#if DEBUG
                            else
                                Console.WriteLine($"DISTANCE PARSE FAILED: {cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].Options}  at: {cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].GetDistance().ToString("0.000")}");
#endif  // DEBUG
                            ++this.lastProcessedPacenoteIdx;
                            continue;
                        }
                        else if (Math.Abs(mainPacenoteDist - cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].GetDistance()) < this.chainedPacenoteThresholdMeters)
                        {
                            if (CoDriver.terminologies.chainedNotes.Contains(cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].GetPacenoteType().ToString()))
                            {
                                Console.WriteLine($"Playing inserted chained pacenote: {CoDriver.PacenoteType.detail_into}  at: {mainPacenoteDist.ToString("0.000")}");
                                foreach (var pacenoteMessageID in this.GetChainedPacenoteMessageIDs(previousPacenoteType, cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].GetPacenoteType(),
                                    mainPacenoteDist, nextBatchDistance, fragmentsInCurrBatch, currentSpeed, cgs.Now))
                                    this.audioPlayer.playMessageImmediately(new QueuedMessage(pacenoteMessageID, 0));

                                // play modifiers for this chained note
                                playModifiers(cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].GetModifier(), cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].GetDistance(),
                                    mainPacenoteDist, nextBatchDistance, fragmentsInCurrBatch, currentSpeed, cgs.Now);
                                this.historicCalls.AddLast(new HistoricCall(cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx], cgs.Now));
                                pacenotesInBatch.Add(cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx]);
                                previousDistance = cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].GetDistance();
                                ++this.lastProcessedPacenoteIdx;

                                continue;
                            }
                        }
                        break;
                    }
                    // we now process the next note in the current set. Before we do, recalcuate the readDist and nextBatchDistance.
                    // This allows us to adjust read ahead distance to take into account the likelihood of the player having to slow
                    // significantly for a tight corner - currentSpeed may be much higher than the actual speed when he reaches a hairpin.
                    // This effectively delays calls after a slow corner to give the driver time to negotiate it before we make subsequent calls
                    if (dynamicLookahead)
                    {
                        readDist = cgs.PositionAndMotionData.DistanceRoundTrack + this.lookaheadSecondsToUse * GetSlowestExpectedSpeedForBatch(pacenotesInBatch, currentSpeed); ;
                        nextBatchDistance = this.FindNextBatchDistance(readDist, cgs, out fragmentsInCurrBatch);
                    }
                }

                if (pacenotesInBatch.Count > 0)
                {
                    this.lastPlayedOrAddedBatch.Clear();
                    this.lastPlayedOrAddedBatch.AddRange(pacenotesInBatch);
                    this.lastPlayedBatchTime = cgs.Now;
                }
#if DEBUG
                if (this.lastProcessedPacenoteIdx < cgs.CoDriverPacenotes.Count
                    && !reachedFinish)
                    Debug.Assert(nextBatchDistance == cgs.CoDriverPacenotes[this.lastProcessedPacenoteIdx].GetDistance());
#endif  // DEBUG
            }
        }

        private void playModifiers(CoDriver.PacenoteModifier modifier, float distance, float mainPacenoteDist, float nextBatchDistance, int fragmentsInCurrBatch, float speed, DateTime now)
        {
            if (modifier != CoDriver.PacenoteModifier.none)
            {
                foreach (var mod in Utilities.GetEnumFlags(modifier))
                {
                    if ((CoDriver.PacenoteModifier)mod != CoDriver.PacenoteModifier.none)
                    {
                        Console.WriteLine($"Playing modifier for pacenote: {mod}  at: {distance.ToString("0.000")}");
                        if (Enum.TryParse<CoDriver.PacenoteModifier>(mod.ToString(), out var modChecked))
                            this.audioPlayer.playMessageImmediately(new QueuedMessage(this.GetPacenoteMessageID(CoDriver.PacenoteType.unknown, mainPacenoteDist, nextBatchDistance, fragmentsInCurrBatch, speed, now, modChecked), 0));
#if DEBUG
                        else
                            Console.WriteLine($"MODIFIER PARSE FAILED: {mod}  at: {distance.ToString("0.000")}");
#endif  // DEBUG
                    }
                }
            }
        }

        // prevent too many 'into' sounds being stacked up in a single block of messages and block some specific combinations
        private bool canUseChaining(DateTime now, PacenoteType previousPacenoteType, PacenoteType pacenoteType)
        {
            if (IsCorner(previousPacenoteType) && pacenoteType == PacenoteType.detail_junction)
            {
                // don't allow "into junction" after a corner call as the corner will probably be the junction
                return false;
            }
            if (IsCorner(previousPacenoteType) &&
                (pacenoteType == PacenoteType.detail_ruts || pacenoteType == PacenoteType.detail_bumps || pacenoteType == PacenoteType.detail_bumpy || pacenoteType == PacenoteType.detail_deepruts))
            {
                // don't allow "into ruts" after a corner call as the ruts / bumps will probably be at the corner
                return false;
            }
            if (previousPacenoteType == PacenoteType.detail_into || previousPacenoteType == PacenoteType.detail_and || previousPacenoteType == PacenoteType.detail_then)
            {
                return false;
            }
            return (now - lastIntoSoundPlayed).TotalSeconds > 2;
        }

        private List<string> GetChainedPacenoteMessageIDs(PacenoteType previousPaceNoteType, PacenoteType pacenoteType, float mainPacenoteDist,
            float nextBatchDistance, int fragmentsInCurrBatch, float speed, DateTime now)
        {
            var IDs = new List<string>();
            var id = this.GetPacenoteMessageID(pacenoteType, mainPacenoteDist, nextBatchDistance, fragmentsInCurrBatch, speed, now);

            if (canUseChaining(now, previousPaceNoteType, pacenoteType))
            {

                // first see if we have a compound "into_[whatever] sound, and if so use it:
                var idWithIntoPrefix = id.Insert(CoDriver.folderCodriverPrefix.Length, "cmp_into_");
#if DEBUG
                Console.WriteLine($"LOOKING FOR {idWithIntoPrefix}");
#endif  // DEBUG
                if (SoundCache.availableSounds.Contains(idWithIntoPrefix))
                {
#if DEBUG
                    Console.WriteLine($"PLAYING COMPOUND INSERTED PACENOTE: {idWithIntoPrefix}");
#endif  // DEBUG
                    IDs.Add(idWithIntoPrefix);
                }
                else if (intoToOver.ContainsKey(pacenoteType))
                {
                    // otherwise see if we can transform into X to over X
#if DEBUG
                    Console.WriteLine($"TRANSFORMING: into_{pacenoteType} TO over_{pacenoteType}");
#endif  // DEBUG
                    IDs.Add(this.GetPacenoteMessageID(intoToOver[pacenoteType], mainPacenoteDist, nextBatchDistance, fragmentsInCurrBatch, speed, now));
                }
                else
                {
                    // just add the into and the X messages
#if DEBUG
                    Console.WriteLine("Falling back to INTO");
#endif  // DEBUG
                    IDs.Add(this.GetPacenoteMessageID(CoDriver.PacenoteType.detail_into, mainPacenoteDist, nextBatchDistance, fragmentsInCurrBatch, speed, now));
                    IDs.Add(id);
                }
                lastIntoSoundPlayed = now;
            }
            else
            {
                IDs.Add(id);
            }

            return IDs;
        }

        public float FindNextBatchDistance(float readDist, GameStateData cgs, out int fragmentsInCurrBatch)
        {
            var preprocPacenoteIdx = this.lastProcessedPacenoteIdx;
            var reachedFinish = false;

            fragmentsInCurrBatch = 0;

            while (preprocPacenoteIdx < cgs.CoDriverPacenotes.Count
                && readDist > cgs.CoDriverPacenotes[preprocPacenoteIdx].GetDistance())
            {
                if (this.ShouldIgnorePacenote(cgs.CoDriverPacenotes[preprocPacenoteIdx]))
                {
                    ++preprocPacenoteIdx;
                    continue;
                }

                // Finish is special: do not look ahead.
                if (cgs.CoDriverPacenotes[preprocPacenoteIdx].GetPacenoteType() == CoDriver.PacenoteType.detail_finish
                    && cgs.PositionAndMotionData.DistanceRoundTrack < cgs.CoDriverPacenotes[preprocPacenoteIdx].GetDistance())
                {
                    reachedFinish = true;
                    break;
                }

                var prevNoteDist = cgs.CoDriverPacenotes[preprocPacenoteIdx].GetDistance();

                // Handle modifiers.
                var modifier = cgs.CoDriverPacenotes[preprocPacenoteIdx].GetModifier();
                if (modifier != CoDriver.PacenoteModifier.none)
                {
                    foreach (var mod in Utilities.GetEnumFlags(modifier))
                    {
                        if ((CoDriver.PacenoteModifier)mod != CoDriver.PacenoteModifier.none)
                        {
                            if (Enum.TryParse<CoDriver.PacenoteModifier>(mod.ToString(), out var modChecked))
                                ++fragmentsInCurrBatch;
                        }
                    }
                }

                ++preprocPacenoteIdx;
                ++fragmentsInCurrBatch;

                // Skip chained calls.
                while (preprocPacenoteIdx < cgs.CoDriverPacenotes.Count)
                {
                    if (cgs.CoDriverPacenotes[preprocPacenoteIdx].GetPacenoteType() == CoDriver.PacenoteType.detail_distance_call)
                    {
                        ++preprocPacenoteIdx;
                        ++fragmentsInCurrBatch;
                        continue;
                    }
                    else if (Math.Abs(prevNoteDist - cgs.CoDriverPacenotes[preprocPacenoteIdx].GetDistance()) < this.chainedPacenoteThresholdMeters)
                    {
                        if (CoDriver.terminologies.chainedNotes.Contains(cgs.CoDriverPacenotes[preprocPacenoteIdx].GetPacenoteType().ToString()))
                        {
                            ++preprocPacenoteIdx;

                            // Count chained pace notes as 2 fragments, as they are typically consist of 'over X' or 'into X'
                            fragmentsInCurrBatch += 2;
                            continue;
                        }
                    }

                    break;
                }
            }

            if (preprocPacenoteIdx < cgs.CoDriverPacenotes.Count
                && !reachedFinish)
                return cgs.CoDriverPacenotes[preprocPacenoteIdx].GetDistance();

            return -1.0f;
        }

        private string GetMessageID(PacenoteType pacenote, PacenoteModifier modifier)
        {
            var pacenoteStr = pacenote != PacenoteType.unknown ? pacenote.ToString() : modifier.ToString();
            var pacenoteID = CoDriver.folderCodriverPrefix + pacenoteStr;
            if (pacenoteStr.StartsWith("corner_"))
            {
                var pacenoteStrRemapped = this.RemapPerChosenTerminology(pacenoteStr, pacenote);
                switch (CoDriver.cornerCallStyle)
                {
                    case CoDriver.CornerCallStyle.NUMBER_FIRST:
                        pacenoteID = CoDriver.folderCodriverPrefix + pacenoteStrRemapped;
                        break;
                    case CoDriver.CornerCallStyle.DIRECTION_FIRST:
                        {
                            pacenoteID = CoDriver.folderCodriverPrefix + pacenoteStrRemapped;

                            var reversedPacenoteString = pacenoteID + "_reversed";
                            if (SoundCache.availableSounds.Contains(reversedPacenoteString))
                                pacenoteID = reversedPacenoteString;
                            else
                                Console.WriteLine($"CoDriver: The sound: '{reversedPacenoteString}' is not available, reverting to: '{pacenoteID}'");
                            break;
                        }
                    case CoDriver.CornerCallStyle.DESCRIPTIVE:
                        {
                            pacenoteID = CoDriver.folderCodriverPrefix + pacenoteStrRemapped;

                            var descriptivePacenoteString = pacenoteID + "_descriptive";
                            if (SoundCache.availableSounds.Contains(descriptivePacenoteString))
                                pacenoteID = descriptivePacenoteString;
                            else
                                Console.WriteLine($"CoDriver: The sound: '{descriptivePacenoteString}' is not available, reverting to: '{pacenoteID}'");
                            break;
                        }
                    case CoDriver.CornerCallStyle.NUMBER_FIRST_REVERSED:
                    case CoDriver.CornerCallStyle.DIRECTION_FIRST_REVERSED:
                        {
                            // Invert 1:6 -> 6:1.
                            if (reverseCorners.ContainsKey(pacenoteStrRemapped))
                            {
                                pacenoteStrRemapped = reverseCorners[pacenoteStrRemapped];
                            }

                            if (CoDriver.cornerCallStyle == CoDriver.CornerCallStyle.DIRECTION_FIRST_REVERSED)
                            {
                                pacenoteID = CoDriver.folderCodriverPrefix + pacenoteStrRemapped;

                                var reversedPacenoteString = pacenoteID + "_reversed";
                                if (SoundCache.availableSounds.Contains(reversedPacenoteString))
                                    pacenoteID = reversedPacenoteString;
                                else
                                    Console.WriteLine($"CoDriver: The sound: '{reversedPacenoteString}' is not available, reverting to: '{pacenoteID}'");
                            }
                            else
                                pacenoteID = CoDriver.folderCodriverPrefix + pacenoteStrRemapped;

                            break;
                        }
                }
            }
            return pacenoteID;
        }

        public string GetPacenoteMessageID(CoDriver.PacenoteType pacenote, float distance, float nextBatchDistance, int fragmentsInCurrBatch, float carSpeed, DateTime now,
            CoDriver.PacenoteModifier modifier = PacenoteModifier.none)
        {
            var pacenoteStr = pacenote != PacenoteType.unknown ? pacenote.ToString() : modifier.ToString();

            var pacenoteID = GetMessageID(pacenote, modifier);

            // TODO: handle relaxed vs regular, and potentially handle complex messages (into glued).
            var distToNextBatch = Math.Abs(distance - nextBatchDistance);

            // For each additional fragment (phrase) in the current batch, increase the threshold to rushed messages.
            var fragmentThreshold = (fragmentsInCurrBatch - 1) * 0.2f;
            var rushedThresholdDistMeters = (this.rushedLookaheadSeconds + fragmentThreshold) * carSpeed;
            var rushedThresholdDistNoFragmentsMeters = this.rushedLookaheadSeconds * carSpeed;

            // rushed pace notes are a bit sticky - if we played a rushed pace note less than 1 second ago, the subsequent note should be rushed
            // even if we'd otherwise not rush it
            bool rushBecauseLastWasRushed = this.lastRushedPacenoteTime.AddSeconds(3.0 + (this.lastBatchFragmentCount - 1) * 0.3) > now;
            // always check if we're legitimately rushing
            bool rushBecauseSpeedOrQueueLength = (this.rushedLookaheadSeconds > 0.01f  // Below consider disabled
                      && distToNextBatch < rushedThresholdDistMeters)
                || carSpeed > rushedSpeedKmH / 3.6f; // above 140km/h

            if (rushBecauseLastWasRushed || rushBecauseSpeedOrQueueLength)
            {
                var rushedPacenoteID = pacenoteID + "_rushed";
                if (SoundCache.availableSounds.Contains(rushedPacenoteID))
                {
                    var reason = "Stickiness";
                    if (rushBecauseSpeedOrQueueLength)
                    {
                        reason = "Speed";
                        if (distToNextBatch < rushedThresholdDistNoFragmentsMeters)
                            reason = "DistNoFragments";
                        else if (distToNextBatch < rushedThresholdDistMeters)
                            reason = "Dist";
                    }

                    Console.WriteLine($"CoDriver: Using rushed version of sound: '{pacenoteID}'  carSpeed: {(carSpeed * 3.6).ToString("0.0")}km/h"
                        + $"  distanceToNextBatch: {distToNextBatch.ToString("0.0")}m  fragmentsInCurrBatch: {fragmentsInCurrBatch}  "
                        + $"  fragmentAddedThreshold: {(fragmentThreshold * carSpeed).ToString("0.0")}m"
                        + $"  rushedThresholdDistMeters: {rushedThresholdDistMeters.ToString("0.0")}m  Reason: {reason}");

                    pacenoteID = rushedPacenoteID;
                    // only update the last rushed pacenote time is this was a legitimately rushed pace note
                    if (rushBecauseSpeedOrQueueLength)
                    {
                        this.lastRushedPacenoteTime = now;
                        this.lastBatchFragmentCount = fragmentsInCurrBatch;
                    }
                }
#if DEBUG
                else
                    Console.WriteLine($"CoDriver: The sound: '{rushedPacenoteID}' is not available, reverting to: '{pacenoteID}'");
#endif  // DEBUG
            }

            return pacenoteID;
        }

        private string RemapPerChosenTerminology(string pacenoteStr, CoDriver.PacenoteType pacenote)
        {
            var mappedPacenoteStr = pacenoteStr;
            // Each style can have multiple terminologies, or at least one.  For now, match Janne's pack, but eventually it could be one more json file with terminology definitions.
            if (CoDriver.terminologies.termininologies.TryGetValue(CoDriver.cornerCallStyle.ToString(), out var terminology))
            {
                if (terminology.terminology.TryGetValue(pacenote.ToString(), out var mappedPacenote))
                    mappedPacenoteStr = mappedPacenote;
            }

#if DEBUG
            if (mappedPacenoteStr != pacenoteStr)
                Console.WriteLine($"PACENOTE RE-MAPPED FROM: {pacenoteStr}  TO: {mappedPacenoteStr}");
#endif  // DEBUG
            return mappedPacenoteStr;
        }

        public void PlayFinishMessage()
        {
            this.audioPlayer.playMessageImmediately(new QueuedMessage(CoDriver.folderCodriverPrefix + CoDriver.PacenoteType.detail_finish, 0));
        }

        private bool ShouldIgnorePacenote(CoDriverPacenote pacenote)
        {
            if (pacenote == null)
            {
                return true;
            }
            switch (pacenote.GetPacenoteType())
            {
                case CoDriver.PacenoteType.detail_start:
                case CoDriver.PacenoteType.detail_empty_call:
                case CoDriver.PacenoteType.detail_split:  // For now, ignore split/checkpoints, but eventually consider announcing something, maybe time, if not busy.
                    return true;
                case CoDriver.PacenoteType.detail_distance_call:
                    return pacenote.Options == null;
            }
            return false;
        }

        private static readonly List<SpeechCommands.ID> Commands = new List<SpeechCommands.ID>
        {
            SpeechCommands.ID.RALLY_CORNER_DECRIPTIONS,
            SpeechCommands.ID.RALLY_CORNER_DIRECTION_FIRST,
            SpeechCommands.ID.RALLY_CORNER_NUMBER_FIRST,
            SpeechCommands.ID.RALLY_CORRECTION,
            SpeechCommands.ID.RALLY_CUT,
            SpeechCommands.ID.RALLY_DISTANCE,
            SpeechCommands.ID.RALLY_DONT_CUT,
            SpeechCommands.ID.RALLY_EARLIER,
            SpeechCommands.ID.RALLY_EARLIER_CALLS,
            SpeechCommands.ID.RALLY_FINISH_RECORDING_STAGE_NOTES,
            SpeechCommands.ID.RALLY_INSERT,
            SpeechCommands.ID.RALLY_LATER,
            SpeechCommands.ID.RALLY_LATER_CALLS,
            SpeechCommands.ID.RALLY_LEFT,
            SpeechCommands.ID.RALLY_LONG,
            SpeechCommands.ID.RALLY_LONGLONG,
            SpeechCommands.ID.RALLY_MAYBE,
            SpeechCommands.ID.RALLY_MINUS,
            SpeechCommands.ID.RALLY_NARROWS,
            SpeechCommands.ID.RALLY_OPENS,
            SpeechCommands.ID.RALLY_PLUS,
            SpeechCommands.ID.RALLY_RIGHT,
            SpeechCommands.ID.RALLY_START_RECORDING_STAGE_NOTES,
            SpeechCommands.ID.RALLY_TIGHTENS,
            SpeechCommands.ID.RALLY_TIGHTENS_BAD,
            SpeechCommands.ID.RALLY_TIGHTENS_THEN_OPENS,
            SpeechCommands.ID.RALLY_TIGHTENS_TO_1,
            SpeechCommands.ID.RALLY_TIGHTENS_TO_2,
            SpeechCommands.ID.RALLY_TIGHTENS_TO_3,
            SpeechCommands.ID.RALLY_TIGHTENS_TO_4,
            SpeechCommands.ID.RALLY_TIGHTENS_TO_5,
            SpeechCommands.ID.RALLY_TIGHTENS_TO_HAIRPIN,
            SpeechCommands.ID.RALLY_WIDENS,
            SpeechCommands.ID.REPEAT_LAST_MESSAGE,

            SpeechCommands.ID.RALLY_1,
            SpeechCommands.ID.RALLY_2,
            SpeechCommands.ID.RALLY_3,
            SpeechCommands.ID.RALLY_4,
            SpeechCommands.ID.RALLY_5,
            SpeechCommands.ID.RALLY_6,
            //SpeechCommands.ID.RALLY_AND,
            SpeechCommands.ID.RALLY_BAD_CAMBER,
            SpeechCommands.ID.RALLY_BIG_JUMP,
            SpeechCommands.ID.RALLY_BRAKE,
            SpeechCommands.ID.RALLY_BRIDGE,
            SpeechCommands.ID.RALLY_BUMPS,
            SpeechCommands.ID.RALLY_CARE,
            SpeechCommands.ID.RALLY_CAUTION,
            SpeechCommands.ID.RALLY_CONCRETE,
            SpeechCommands.ID.RALLY_CORNER_DECRIPTIONS,
            SpeechCommands.ID.RALLY_CORNER_DIRECTION_FIRST,
            SpeechCommands.ID.RALLY_CORNER_NUMBER_FIRST,
            SpeechCommands.ID.RALLY_CORRECTION,
            SpeechCommands.ID.RALLY_CREST,
            SpeechCommands.ID.RALLY_CUT,
            SpeechCommands.ID.RALLY_DANGER,
            SpeechCommands.ID.RALLY_DEEP_RUTS,
            SpeechCommands.ID.RALLY_DIP,
            SpeechCommands.ID.RALLY_DISTANCE,
            SpeechCommands.ID.RALLY_DONT_CUT,
            SpeechCommands.ID.RALLY_DOUBLE_CAUTION,
            SpeechCommands.ID.RALLY_DOWNHILL,
            SpeechCommands.ID.RALLY_EARLIER,
            SpeechCommands.ID.RALLY_EARLIER_CALLS,
            SpeechCommands.ID.RALLY_FINISH_RECORDING_STAGE_NOTES,
            SpeechCommands.ID.RALLY_FLAT,
            SpeechCommands.ID.RALLY_FORD,
            SpeechCommands.ID.RALLY_GO_STRAIGHT,
            SpeechCommands.ID.RALLY_GRAVEL,
            SpeechCommands.ID.RALLY_HAIRPIN,
            SpeechCommands.ID.RALLY_INSERT,
            SpeechCommands.ID.RALLY_INTO,
            SpeechCommands.ID.RALLY_JUMP,
            SpeechCommands.ID.RALLY_JUNCTION,
            SpeechCommands.ID.RALLY_KEEP_IN,
            SpeechCommands.ID.RALLY_KEEP_LEFT,
            SpeechCommands.ID.RALLY_KEEP_MIDDLE,
            SpeechCommands.ID.RALLY_KEEP_OUT,
            SpeechCommands.ID.RALLY_KEEP_RIGHT,
            SpeechCommands.ID.RALLY_LATER,
            SpeechCommands.ID.RALLY_LATER_CALLS,
            SpeechCommands.ID.RALLY_LEFT,
            SpeechCommands.ID.RALLY_LEFT_ENTRY_CHICANE,
            SpeechCommands.ID.RALLY_LOGS_INSIDE,
            SpeechCommands.ID.RALLY_LOGS_OUTSIDE,
            SpeechCommands.ID.RALLY_LONG,
            SpeechCommands.ID.RALLY_LONGLONG,
            SpeechCommands.ID.RALLY_LOOSE_GRAVEL,
            SpeechCommands.ID.RALLY_MAYBE,
            SpeechCommands.ID.RALLY_MINUS,
            SpeechCommands.ID.RALLY_NARROWS,
            SpeechCommands.ID.RALLY_OPEN_HAIRPIN,
            SpeechCommands.ID.RALLY_OPENS,
            SpeechCommands.ID.RALLY_OPENS_THEN_TIGHTENS,
            SpeechCommands.ID.RALLY_OVER_BRIDGE,
            SpeechCommands.ID.RALLY_OVER_CREST,
            SpeechCommands.ID.RALLY_OVER_JUMP,
            SpeechCommands.ID.RALLY_OVER_RAILS,
            SpeechCommands.ID.RALLY_PLUS,
            SpeechCommands.ID.RALLY_RIGHT,
            SpeechCommands.ID.RALLY_RIGHT_ENTRY_CHICANE,
            SpeechCommands.ID.RALLY_ROCKS_INSIDE,
            SpeechCommands.ID.RALLY_ROCKS_OUTSIDE,
            SpeechCommands.ID.RALLY_RUTS,
            SpeechCommands.ID.RALLY_SLIPPY,
            SpeechCommands.ID.RALLY_SNOW,
            SpeechCommands.ID.RALLY_SQUARE,
            SpeechCommands.ID.RALLY_START_RECORDING_STAGE_NOTES,
            SpeechCommands.ID.RALLY_TARMAC,
            SpeechCommands.ID.RALLY_THEN,
            SpeechCommands.ID.RALLY_THROUGH_GATE,
            SpeechCommands.ID.RALLY_TIGHTENS,
            SpeechCommands.ID.RALLY_TIGHTENS_BAD,
            SpeechCommands.ID.RALLY_TIGHTENS_THEN_OPENS,
            SpeechCommands.ID.RALLY_TIGHTENS_TO_1,
            SpeechCommands.ID.RALLY_TIGHTENS_TO_2,
            SpeechCommands.ID.RALLY_TIGHTENS_TO_3,
            SpeechCommands.ID.RALLY_TIGHTENS_TO_4,
            SpeechCommands.ID.RALLY_TIGHTENS_TO_5,
            SpeechCommands.ID.RALLY_TIGHTENS_TO_HAIRPIN,
            SpeechCommands.ID.RALLY_TREE_INSIDE,
            SpeechCommands.ID.RALLY_TREE_OUTSIDE,
            SpeechCommands.ID.RALLY_TUNNEL,
            SpeechCommands.ID.RALLY_TWISTY,
            SpeechCommands.ID.RALLY_UPHILL,
            SpeechCommands.ID.RALLY_WIDE_OUT,
            SpeechCommands.ID.RALLY_WIDENS,

        };
        private static List<SpeechCommands.ID> ControllerCommands = new List<SpeechCommands.ID>
        {
            SpeechCommands.ID.TOGGLE_RALLY_RECCE_MODE
        };
        public override SpeechCommands.ID HandlesEvent(String voiceMessage)
        {
            SpeechCommands.ID cmd = SpeechCommands.SpeechToCommand(Commands, voiceMessage);
            if (cmd == SpeechCommands.ID.NO_COMMAND)
            {
                cmd = SpeechCommands.ControllerToCommand(ControllerCommands, voiceMessage);
            }
            return cmd;
        }

        public override void respond(String voiceMessage)
        {
            respond(voiceMessage, HandlesEvent(voiceMessage));
        }
        public override void respond(String voiceMessage, SpeechCommands.ID cmd)
        {
            if (GlobalBehaviourSettings.racingType != CrewChief.RacingType.Rally)
            {
                return;
            }
            if (!Commands.Contains(cmd) && !ControllerCommands.Contains(cmd))
            {
                Log.Error($"{cmd.ToString()} not handled by {this.GetType().Name}");
                return;
            }

            switch (cmd)
            {
                case SpeechCommands.ID.RALLY_EARLIER_CALLS:
                {
                    if (this.lookaheadSecondsToUse + earlierLaterStepSeconds <= CoDriver.maxLookaheadSeconds)
                    {
                        this.audioPlayer.playMessageImmediately(new QueuedMessage(this.folderAcknowlegeOK, 0));
                        var newLookahead = this.lookaheadSecondsToUse + earlierLaterStepSeconds;
                        Console.WriteLine("Increasing lookahead from " + this.lookaheadSecondsToUse.ToString("0.0") + " seconds to " + newLookahead.ToString("0.0") + " seconds.");
                        this.lookaheadSecondsToUse = newLookahead;
                    }
                    else
                    {
                        // TODO: need to specific "no, bugger off" response?
                        this.audioPlayer.playMessageImmediately(new QueuedMessage(this.folderNo, 0));
                    }

                    break;
                }
                case SpeechCommands.ID.RALLY_LATER_CALLS:
                {
                    if (this.lookaheadSecondsToUse - earlierLaterStepSeconds >= CoDriver.minLookaheadSeconds)
                    {
                        this.audioPlayer.playMessageImmediately(new QueuedMessage(this.folderAcknowlegeOK, 0));
                        var newLookahead = this.lookaheadSecondsToUse - earlierLaterStepSeconds;
                        Console.WriteLine("Decreasing lookahead from " + this.lookaheadSecondsToUse.ToString("0.0") + " seconds to " + newLookahead.ToString("0.0") + " seconds.");
                        this.lookaheadSecondsToUse = newLookahead;
                    }
                    else
                    {
                        // TODO: need to specific "no, bugger off" response?
                        this.audioPlayer.playMessageImmediately(new QueuedMessage(this.folderNo, 0));
                    }

                    break;
                }
                case SpeechCommands.ID.RALLY_CORNER_DECRIPTIONS:
                    this.audioPlayer.playMessageImmediately(new QueuedMessage(this.folderAcknowlegeOK, 0));
                    CoDriver.cornerCallStyle = CornerCallStyle.DESCRIPTIVE;
                    break;
                case SpeechCommands.ID.RALLY_CORNER_NUMBER_FIRST:
                    this.audioPlayer.playMessageImmediately(new QueuedMessage(this.folderAcknowlegeOK, 0));
                    CoDriver.cornerCallStyle = this.preferReversedNumbers ? CornerCallStyle.NUMBER_FIRST_REVERSED : CornerCallStyle.NUMBER_FIRST;
                    break;
                case SpeechCommands.ID.RALLY_CORNER_DIRECTION_FIRST:
                    this.audioPlayer.playMessageImmediately(new QueuedMessage(this.folderAcknowlegeOK, 0));
                    CoDriver.cornerCallStyle = this.preferReversedNumbers ? CornerCallStyle.DIRECTION_FIRST_REVERSED : CornerCallStyle.DIRECTION_FIRST;
                    break;
                case SpeechCommands.ID.RALLY_START_RECORDING_STAGE_NOTES:
                {
                    if (!this.inReceMode)
                    {
                        this.inReceMode = true;
                        this.recePaceNotes.Clear();
                        if (CrewChief.currentGameState != null)
                        {
                            CrewChief.currentGameState.CoDriverPacenotes.Clear();
                        }
                    }
                    this.audioPlayer.playMessageImmediately(new QueuedMessage(this.folderAcknowledgeStartRecce, 0));
                    break;
                }
                case SpeechCommands.ID.RALLY_FINISH_RECORDING_STAGE_NOTES:
                {
                    if (this.inReceMode)
                    {
                        this.inReceMode = false;
                        if (this.recePaceNotes != null && this.recePaceNotes.Count > 0)
                        {
                            WriteRecePacenotes(lastStageName);
                        }
                        // weird bug: after finishing stage recce mid-stage, the app spews loads of pace note messages

                        if (CrewChief.currentGameState != null)
                        {
                            this.lastProcessedPacenoteIdx = CrewChief.currentGameState.CoDriverPacenotes.Count - 1;
                            CrewChief.currentGameState.CoDriverPacenotes.Clear();
                        }
                        this.recePaceNotes.Clear();
                    }
                    this.audioPlayer.playMessageImmediately(new QueuedMessage(this.folderAcknowledgeEndRecce, 0));
                    break;
                }
                case SpeechCommands.ID.TOGGLE_RALLY_RECCE_MODE:
                {
                    if (!this.inReceMode)
                    {
                        this.inReceMode = true;
                        this.recePaceNotes.Clear();
                        if (CrewChief.currentGameState != null)
                        {
                            CrewChief.currentGameState.CoDriverPacenotes.Clear();
                        }
                        this.audioPlayer.playMessageImmediately(new QueuedMessage(this.folderAcknowledgeStartRecce, 0));
                    }
                    else
                    {
                        this.inReceMode = false;
                        if (this.recePaceNotes != null && this.recePaceNotes.Count > 0)
                        {
                            WriteRecePacenotes(lastStageName);
                            // weird bug: after finishing stage recce mid-stage, the app spews loads of pace note messages
                            if (CrewChief.currentGameState != null)
                            {
                                this.lastProcessedPacenoteIdx = CrewChief.currentGameState.CoDriverPacenotes.Count - 1;
                                CrewChief.currentGameState.CoDriverPacenotes.Clear();
                            }
                            this.recePaceNotes.Clear();
                        }
                        this.audioPlayer.playMessageImmediately(new QueuedMessage(this.folderAcknowledgeEndRecce, 0));
                    }
                    break;
                }
                case SpeechCommands.ID.RALLY_CORRECTION:
                {
                    if (this.inReceMode)
                    {
                        // save the previously added batch from the recce notes - these will be re-added if the correction doesn't
                        // insert any replacements
                        List<CoDriverPacenote> deletedPacenotes = new List<CoDriverPacenote>();
                        // remove the previously added batch from the recce notes
                        float distanceOfReplacedBatch = -1;
                        foreach (CoDriverPacenote pacenote in this.lastPlayedOrAddedBatch)
                        {
                            Console.WriteLine("Recce correction, removing pace note " + pacenote);
                            this.recePaceNotes.Remove(pacenote);
                            deletedPacenotes.Add(pacenote);
                            distanceOfReplacedBatch = pacenote.Distance;
                        }
                        // remove the correction bit from the voice command
                        foreach (string correctionFragment in SpeechRecogniser.RALLY_CORRECTION)
                        {
                            if (voiceMessage.StartsWith(correctionFragment))
                            {
                                voiceMessage = voiceMessage.Remove(0, correctionFragment.Length).Trim();
                                break;
                            }
                        }
                        bool moveEarlier = SpeechRecogniser.ResultContains(voiceMessage, SpeechRecogniser.RALLY_EARLIER);
                        bool moveLater = SpeechRecogniser.ResultContains(voiceMessage, SpeechRecogniser.RALLY_LATER);
                        // now process this as a regular recce note
                        if (ProcessRecePaceNote(voiceMessage, false))
                        {
                            if (distanceOfReplacedBatch == -1)
                                distanceOfReplacedBatch = this.lastPlayedOrAddedBatch[0].Distance;
                            foreach (CoDriverPacenote pacenote in this.lastPlayedOrAddedBatch)
                            {
                                pacenote.Distance = moveEarlier ? Math.Max(0, distanceOfReplacedBatch - 50) : moveLater ? distanceOfReplacedBatch + 50 : distanceOfReplacedBatch;
                            }
                            if (UserSettings.GetUserSettings().getBoolean("confirm_recce_pace_notes"))
                            {
                                this.audioPlayer.playMessageImmediately(new QueuedMessage(this.folderCorrection, 0));
                                ReplayLastPacenotesBatch(false);
                            }
                        }
                        else
                        {
                            // got 'correction' but no actual correction, reinstate the removed notes and say "eh?"
                            foreach (CoDriverPacenote pacenote in deletedPacenotes)
                            {
                                if (moveEarlier)
                                    pacenote.Distance = Math.Max(0, pacenote.Distance - 50);
                                else if (moveLater)
                                    pacenote.Distance = pacenote.Distance + 50;
                            }
                            this.recePaceNotes.AddRange(deletedPacenotes);
                            if (!moveLater && !moveEarlier)
                            {
                                Console.WriteLine("Voice message \"Correction, " + voiceMessage + "\" didn't produce any pace notes");
                                this.audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderDidntUnderstand, 0));
                            }
                            else
                            {
                                this.audioPlayer.playMessageImmediately(new QueuedMessage(this.folderAcknowlegeOK, 0));
                            }
                        }
                    }
                    else
                    {
                        // regular non-recce correction
                        ProcessCorrection(voiceMessage);
                    }

                    break;
                }
                case SpeechCommands.ID.RALLY_INSERT:
                    ProcessInsert(voiceMessage);
                    break;
                case SpeechCommands.ID.REPEAT_LAST_MESSAGE:
                    ReplayLastPacenotesBatch(false);
                    break;
                default:
                {
                    if (this.inReceMode)
                    {
                        bool addedPacenote = ProcessRecePaceNote(voiceMessage, true);
                        if (addedPacenote)
                        {
                            if (UserSettings.GetUserSettings().getBoolean("confirm_recce_pace_notes"))
                            {
                                ReplayLastPacenotesBatch(true);
                            }
                        }
                        else
                        {
                            Console.WriteLine("Voice message \"" + voiceMessage + "\" didn't produce any pace notes");
                            this.audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderDidntUnderstand, 0));
                        }
                    }

                    break;
                }
            }
        }

        private void ReplayLastPacenotesBatch(bool addAcknowledge)
        {
            if (this.lastPlayedOrAddedBatch.Count > 0)
            {
                List<MessageFragment> confirmationFragments = new List<MessageFragment>();
                if (addAcknowledge)
                {
                    confirmationFragments.Add(MessageFragment.Text(AudioPlayer.folderAcknowlegeOK));
                }
                foreach (CoDriverPacenote pacenote in this.lastPlayedOrAddedBatch)
                {
                    if (pacenote.GetPacenoteType() != PacenoteType.detail_distance_call && pacenote.GetPacenoteType() != PacenoteType.unknown)
                    {
                        string sound = GetMessageID(pacenote.GetPacenoteType(), PacenoteModifier.none);
                        if (SoundCache.availableSounds.Contains(sound))
                        {
                            confirmationFragments.Add(MessageFragment.Text(sound));
                        }
                        else
                        {
                            Console.WriteLine("Successfully identified pace note modifier " + sound + " but can't find a sound for this");
                        }
                    }
                    if (pacenote.GetModifier() != PacenoteModifier.none)
                    {
                        foreach (var mod in Utilities.GetEnumFlags(pacenote.GetModifier()))
                        {
                            if ((CoDriver.PacenoteModifier)mod != CoDriver.PacenoteModifier.none)
                            {
                                if (Enum.TryParse<CoDriver.PacenoteModifier>(mod.ToString(), out var modChecked))
                                {
                                    string sound = GetMessageID(PacenoteType.unknown, modChecked);
                                    if (SoundCache.availableSounds.Contains(sound))
                                    {
                                        confirmationFragments.Add(MessageFragment.Text(sound));
                                    }
                                    else
                                    {
                                        Console.WriteLine("Successfully identified pace note fragment " + sound + " but can't find a sound for this");
                                    }
                                }
                            }
                        }
                    }
                }
                this.audioPlayer.playMessageImmediately(new QueuedMessage("pacenote confirmation", 0, confirmationFragments));
            }
            else if (addAcknowledge)
            {
                this.audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderDidntUnderstand, 0));
            }
        }

        private void WriteRecePacenotes(string trackName)
        {
            string pacenotesPath = GetPacenotesPath(trackName, false);
            Directory.CreateDirectory(pacenotesPath);
            RenameExistingPacenotesFile(trackName);
            File.WriteAllText(Path.Combine(pacenotesPath, CoDriver.pacenotesFileName), JsonConvert.SerializeObject(this.recePaceNotes, Formatting.Indented));
        }

        // we allow ambiguous Dirt track names when loading but not when saving
        private string GetPacenotesPath(string trackName, bool allowAmbiguousPath)
        {
            trackName = Utilities.FileNames.MakeValidForPathName(trackName);
            string path = Path.Combine(DataFiles.BaseFolder, CrewChief.gameDefinition.gameEnum.ToString(), trackName);
            if (allowAmbiguousPath && (Game.DIRT || Game.DIRT_2))
            {
                // special case for dirt / dirt 2, try and load the stage name with the x and z positions and fall back to the ambiguous name if it's not there
                if (!Directory.Exists(path) && trackName.Contains("^"))
                {
                    string ambiguousTrackName = trackName.Split('^')[0];
                    string ambiguousPath = Path.Combine(DataFiles.BaseFolder, CrewChief.gameDefinition.gameEnum.ToString(), ambiguousTrackName);
                    if (Directory.Exists(ambiguousPath))
                    {
                        Console.WriteLine("Warning: using ambiguous track name " + ambiguousTrackName + " instead of full track name " + trackName);
                        return ambiguousPath;
                    }
                }
            }
            return path;
        }

        private void RenameExistingPacenotesFile(string trackName)
        {
            string pacenotesPath = GetPacenotesPath(trackName, false);
            string existingPacenotesFullPath = Path.Combine(pacenotesPath, CoDriver.pacenotesFileName);
            if (File.Exists(existingPacenotesFullPath))
            {
                string newFileName;
                int i = 1;
                do
                {
                    string filenameWithNoExtension = Path.GetFileNameWithoutExtension(CoDriver.pacenotesFileName);
                    string filenameExtension = Path.GetExtension(CoDriver.pacenotesFileName);
                    newFileName = Path.Combine(pacenotesPath, filenameWithNoExtension + "_" + i + filenameExtension);
                    i++;
                }
                while (File.Exists(newFileName));
                File.Move(existingPacenotesFullPath, newFileName);
            }
        }

        private bool ProcessRecePaceNote(string voiceMessage, bool createPlaceholderForUnrecognised)
        {
            Console.WriteLine("Got stage recce voice message \"" + voiceMessage + "\"");
            float currentDistance = CrewChief.currentGameState == null ? 0 : CrewChief.currentGameState.PositionAndMotionData.DistanceRoundTrack;
            float distance = MainWindow.voiceOption == MainWindow.VoiceOptionEnum.ALWAYS_ON ?
                currentDistance : SpeechRecogniser.distanceWhenVoiceCommandStarted;
            if (SpeechRecogniser.ResultContains(voiceMessage, SpeechRecogniser.RALLY_DISTANCE))
            {
                if (lastRecePacenoteWasDistance)
                {
                    Console.WriteLine("Skipping distance pacenote as we can't have 2 of these consecutively");
                    return true;
                }
                // manually add a distance call - the actual distance will be resolved on playback
                this.recePaceNotes.Add(new CoDriverPacenote { Pacenote = PacenoteType.detail_distance_call, Distance = distance });
                lastRecePacenoteWasDistance = true;
                return true;
            }
            else
            {
                // we're assuming that the pace note command is made *after* the obstacle / corner, so create the notes then use the created
                // notes to estimate how long the obstacle / corner is (i.e. the stage distance when the obstacle starts), and set that into the notes
                List<CoDriverPacenote> paceNotesToAdd = GetPacenotesFromVoiceCommand(voiceMessage, createPlaceholderForUnrecognised, false);

                // see if we need to re-run the parser with a tweaked input phrase for some special cases
                if (retryWithModifiedPhrase(voiceMessage, paceNotesToAdd, out string modifiedVoiceCommand) && modifiedVoiceCommand != null && modifiedVoiceCommand != voiceMessage)
                {
                    return ProcessRecePaceNote(modifiedVoiceCommand, createPlaceholderForUnrecognised);
                }

                // one special case (eeewww). We missed a modifier during our previous corner call and have made a new command which is just "don't cut"
                // or something. In this case we attempt to insert that modifier into the last corner call
                if (paceNotesToAdd.Count == 1 && paceNotesToAdd[0].Pacenote == PacenoteType.unknown && paceNotesToAdd[0].Modifier == PacenoteModifier.none && ContainsCornerModifier(voiceMessage))
                {
                    AppendModifierToLastCorner(voiceMessage);
                }
                else if (paceNotesToAdd.Count > 0)
                {
                    float distanceAtStartOfObstacle = distance - EstimateObstacleLength(paceNotesToAdd);
                    foreach (CoDriverPacenote paceNote in paceNotesToAdd)
                    {
                        paceNote.Distance = distanceAtStartOfObstacle;
                    }
                    // after each block of pace notes there'll be a distance placeholder. We always add this but the decision as to whether it'll be
                    // read is made on playback.
                    if (!lastRecePacenoteWasDistance)
                    {
                        // auto generate an optional distance note
                        paceNotesToAdd.Add(new CoDriverPacenote { Pacenote = PacenoteType.detail_distance_call, Distance = distance + 20 }); // the distance call needs to come some way after the obstacle is finished
                    }
                    this.recePaceNotes.AddRange(paceNotesToAdd);
                    // store these so we can remove them if we get a 'correction' call
                    this.lastPlayedOrAddedBatch.Clear();
                    this.lastPlayedOrAddedBatch.AddRange(paceNotesToAdd);
                    this.lastPlayedBatchTime = CrewChief.currentGameState.Now;
                }
                lastRecePacenoteWasDistance = false;
                return paceNotesToAdd.Count > 0;
            }
        }

        // magic hack for "four" being mis-recognised as "ford". Yuk
        private bool retryWithModifiedPhrase(string voiceMessage, List<CoDriverPacenote> paceNotesToAdd, out string modifiedPhrase)
        {
            // special case for four / ford confusion
            if (paceNotesToAdd.Count > 0 && paceNotesToAdd[0].UnprocessedVoiceCommandText != null)
            {
                if (paceNotesToAdd[0].UnprocessedVoiceCommandText.Contains("left") && voiceMessage.Contains("left ford"))
                {
                    modifiedPhrase = voiceMessage.Replace("left ford", "left four");
                    return true;
                }
                if (paceNotesToAdd[0].UnprocessedVoiceCommandText.Contains("left") && voiceMessage.Contains("ford left"))
                {
                    modifiedPhrase = voiceMessage.Replace("ford left", "four left");
                    return true;
                }
                if (paceNotesToAdd[0].UnprocessedVoiceCommandText.Contains("right") && voiceMessage.Contains("right ford"))
                {
                    modifiedPhrase = voiceMessage.Replace("right ford", "right four");
                    return true;
                }
                if (paceNotesToAdd[0].UnprocessedVoiceCommandText.Contains("right") && voiceMessage.Contains("ford right"))
                {
                    modifiedPhrase = voiceMessage.Replace("ford right", "four right");
                    return true;
                }
            }
            modifiedPhrase = null;
            return false;
        }

        private void AppendModifierToLastCorner(string voiceMessage)
        {
            List<VoiceMessagePaceNoteResult> cornerPaceNotesWithModifiers = GetCornerPacenoteTypesWithModifiers(new MutableString(voiceMessage), true);
            if (cornerPaceNotesWithModifiers.Count == 1 && cornerPaceNotesWithModifiers[1].pacenoteModifier != PacenoteModifier.none)
            {
                // we have a modifier from the call, so see if we have a corner in the last batch and apply the modifier to it
                foreach (CoDriverPacenote paceNote in this.lastPlayedOrAddedBatch)
                {
                    if (IsCorner(paceNote.Pacenote))
                    {
                        // update the modifier
                        paceNote.Modifier = paceNote.Modifier | cornerPaceNotesWithModifiers[1].pacenoteModifier;
                        break;
                    }
                }
            }
        }

        // gets the longest obstacle length from the batch (estimated)
        private float EstimateObstacleLength(List<CoDriverPacenote> paceNotesInBatch)
        {
            float distance = 10;    // any non-corner obstacle is assumed to be 10 metres from the start point to where the player makes the pace note command
            // first see if we have a 'tightens to...' note among the corners
            bool hasTightensTo = false;
            foreach (CoDriverPacenote paceNote in paceNotesInBatch)
            {
                if (paceNote.GetPacenoteType().ToString().Contains("tightens_to"))
                {
                    hasTightensTo = true;
                    break;
                }
            }
            foreach (CoDriverPacenote paceNote in paceNotesInBatch)
            {
                bool isVeryLong = paceNote.GetModifier().ToString().Contains("longlong");
                bool isLong = paceNote.GetModifier().ToString().Contains("long") || paceNote.GetModifier().ToString().Contains("tighens") || hasTightensTo;
                switch (paceNote.GetPacenoteType())
                {
                    case PacenoteType.corner_1_left:
                    case PacenoteType.corner_1_right:
                    case PacenoteType.corner_2_left:
                    case PacenoteType.corner_2_right:
                    case PacenoteType.corner_square_left:
                    case PacenoteType.corner_square_right:
                        // short corners
                        distance = Math.Max(distance, isVeryLong ? 60 : isLong ? 40 : 20);
                        break;
                    case PacenoteType.corner_3_left:
                    case PacenoteType.corner_3_right:
                        distance = Math.Max(distance, isVeryLong ? 90 : isLong ? 60 : 30);
                        break;
                    case PacenoteType.corner_open_hairpin_left:
                    case PacenoteType.corner_open_hairpin_right:
                        distance = Math.Max(distance, isVeryLong ? 140 : isLong ? 100 : 60);
                        break;
                    case PacenoteType.corner_4_left:
                    case PacenoteType.corner_4_right:
                    case PacenoteType.corner_left_acute:
                    case PacenoteType.corner_right_acute:
                    case PacenoteType.detail_left_entry_chicane:
                    case PacenoteType.detail_right_entry_chicane:
                        distance = Math.Max(distance, isVeryLong ? 120 : isLong ? 80 : 50);
                        break;
                    case PacenoteType.corner_5_left:
                    case PacenoteType.corner_5_right:// long corners
                        distance = Math.Max(distance, isVeryLong ? 150 : isLong ? 100 : 50);
                        break;
                    case PacenoteType.corner_6_left:
                    case PacenoteType.corner_6_right:
                    case PacenoteType.corner_flat_left:
                    case PacenoteType.corner_flat_right:
                        // long corners
                        distance = Math.Max(distance, isVeryLong ? 200 : isLong ? 100 : 50);
                        break;
                    case PacenoteType.detail_tunnel:
                        distance = Math.Max(distance, 50);
                        break;
                        // other cases?
                }
            }
            return distance;
        }

        private void ProcessInsert(string voiceMessage)
        {
            Console.WriteLine("Got insert voice message \"" + voiceMessage + "\"");
            if (CrewChief.currentGameState == null || CrewChief.currentGameState.SessionData == null || CrewChief.currentGameState.SessionData.TrackDefinition == null)
            {
                return;
            }
            string trackName = CrewChief.currentGameState.SessionData.TrackDefinition.name;
            float distance = MainWindow.voiceOption == MainWindow.VoiceOptionEnum.ALWAYS_ON ?
                CrewChief.currentGameState.PositionAndMotionData.DistanceRoundTrack : SpeechRecogniser.distanceWhenVoiceCommandStarted;
            // remove the insert bit from the voice command
            foreach (string insertFragment in SpeechRecogniser.RALLY_INSERT)
            {
                if (voiceMessage.StartsWith(insertFragment))
                {
                    voiceMessage = voiceMessage.Remove(0, insertFragment.Length);
                    break;
                }
            }
            List<CoDriverPacenote> insertedNotes = GetPacenotesFromVoiceCommand(voiceMessage, true, false);
            float distanceAtStartOfObstacle = distance - EstimateObstacleLength(insertedNotes);
            foreach (CoDriverPacenote paceNote in insertedNotes)
            {
                paceNote.Distance = distanceAtStartOfObstacle;
            }
            if (insertedNotes.Count > 0)
            {
                this.audioPlayer.playMessageImmediately(new QueuedMessage(this.folderAcknowlegeOK, 0));
                correctionsForCurrentSession.AddRange(insertedNotes);
                WritePacenoteCorrections(trackName);
            }
        }

        // get the pace note (or sometimes multiple pace notes) from a single voice command
        private List<CoDriverPacenote> GetPacenotesFromVoiceCommand(string voiceMessage, bool createPlaceholderForUnrecognised, bool allowModifiersOnly)
        {
            List<CoDriverPacenote> paceNotes = new List<CoDriverPacenote>();
            // as we parse the command we want to consume the recognised text, so wrap this in our little helper class
            MutableString voiceMessageWrapper = new MutableString(voiceMessage);

            // first see if we have a corner - there will only ever be 1 corner and 1 'tightens to...' per command,
            // but ensure we can handle more if needed
            List<int> cornerVoiceCommandMatchLocations = new List<int>();   // these are the voice command text start positions of each corner command
            List<int> cornerPaceNoteLocations = new List<int>();            // these are the corresponding locations in the assembled pace notes list of each corner command
            List<VoiceMessagePaceNoteResult> cornersWithModifiers = GetCornerPacenoteTypesWithModifiers(voiceMessageWrapper, allowModifiersOnly);
            int location = 0;
            foreach (VoiceMessagePaceNoteResult cornerWithModifier in cornersWithModifiers)
            {
                if ((allowModifiersOnly && cornerWithModifier.pacenoteModifier != PacenoteModifier.none) || cornerWithModifier.pacenoteType != PacenoteType.unknown)
                {
                    cornerVoiceCommandMatchLocations.Add(cornerWithModifier.matchStartPoint);
                    cornerPaceNoteLocations.Add(location);
                    location++;
                    paceNotes.Add(new CoDriverPacenote() { Pacenote = cornerWithModifier.pacenoteType, Modifier = cornerWithModifier.pacenoteModifier, RawVoiceCommand = voiceMessage });
                }
            }
            // reset the cursor in the remaining (uneaten) voice message and extract the other obstacle calls
            voiceMessageWrapper.ResetCursor();
            foreach (VoiceMessagePaceNoteResult obstacle in GetObstaclePacenoteTypesWithModifiers(voiceMessageWrapper))
            {
                // use the location hint from the result to decide which of the corners to insert this in front of
                bool inserted = false;
                for (int i = 0; i < cornerPaceNoteLocations.Count; i++)
                {
                    if (!inserted && obstacle.matchStartPoint < cornerVoiceCommandMatchLocations[i])
                    {
                        // this obstacle came before this corner in the voice command so insert it immediately in front of the corner.
                        // Because the list of obstacles is in voice command order doing this more than once will preserve their ordering
                        inserted = true;
                        paceNotes.Insert(cornerPaceNoteLocations[i], new CoDriverPacenote() { Pacenote = obstacle.pacenoteType, Modifier = obstacle.pacenoteModifier, RawVoiceCommand = voiceMessage });
                    }
                    // if we inserted this in front of a corner, move the corner(s) after this location back one
                    if (inserted)
                    {
                        cornerPaceNoteLocations[i] = cornerPaceNoteLocations[i] + 1;
                    }
                }
                // we didn't insert this before a corner, so it goes at the end
                if (!inserted)
                {
                    paceNotes.Add(new CoDriverPacenote() { Pacenote = obstacle.pacenoteType, Modifier = obstacle.pacenoteModifier, RawVoiceCommand = voiceMessage });
                }
            }
            // if we've not been able to work out what's been said here, create an empty pace note to hold the misunderstood raw voice command
            if (paceNotes.Count == 0 && createPlaceholderForUnrecognised)
            {
                paceNotes.Add(new CoDriverPacenote() { RawVoiceCommand = voiceMessage, UnprocessedVoiceCommandText = voiceMessage });
            }
            else
            {
                string uneatenVoiceCommandFragments = voiceMessageWrapper.GetUnprocessedCommandText();
                foreach (CoDriverPacenote paceNote in paceNotes)
                {
                    paceNote.UnprocessedVoiceCommandText = uneatenVoiceCommandFragments;
                }
            }
            return paceNotes;
        }

        private void ProcessCorrection(string voiceMessage)
        {
            Console.WriteLine("Got correction voice message \"" + voiceMessage + "\"");
            if (historicCalls.Last == null || CrewChief.currentGameState == null
                || CrewChief.currentGameState.SessionData == null || CrewChief.currentGameState.SessionData.TrackDefinition == null)
            {
                Console.WriteLine("no pace note to correct");
                return;
            }
            string trackName = CrewChief.currentGameState.SessionData.TrackDefinition.name;
            CoDriver.Direction requestedDirection = Direction.UNKNOWN;
            if (SpeechRecogniser.ResultContains(voiceMessage, SpeechRecogniser.RALLY_LEFT))
            {
                requestedDirection = Direction.LEFT;
            }
            else if (SpeechRecogniser.ResultContains(voiceMessage, SpeechRecogniser.RALLY_RIGHT))
            {
                requestedDirection = Direction.RIGHT;
            }
            List<HistoricCall> callsToCorrect = GetCallsToCorrect(requestedDirection);
            // these are ordered from earliest to latest (so the most recently played call is at the end)
            if (callsToCorrect != null && callsToCorrect.Count > 0)
            {
                // we might change the voice message to replace some contents, so stash it first so we can add the raw message to the note
                string rawVoiceMessage = voiceMessage;
                // check if we're moving a call:
                bool moveEarlier = SpeechRecogniser.ResultContains(voiceMessage, SpeechRecogniser.RALLY_EARLIER);
                bool moveLater = SpeechRecogniser.ResultContains(voiceMessage, SpeechRecogniser.RALLY_LATER);
                // remove that text from the voice command
                foreach (string correctionWord in SpeechRecogniser.RALLY_EARLIER)
                {
                    voiceMessage = voiceMessage.Replace(correctionWord, "").Trim();
                }
                foreach (string correctionWord in SpeechRecogniser.RALLY_LATER)
                {
                    voiceMessage = voiceMessage.Replace(correctionWord, "").Trim();
                }

                bool correctionIncludesCornerModifier = ContainsCornerModifier(voiceMessage);
                // first apply distance corrections - this may create and add corrections that the next step may update
                // We also use this iteration of the historic calls to work out what we could actually correct
                List<HistoricCall> cornersInBatch = new List<HistoricCall>();
                List<HistoricCall> tightensInBatch = new List<HistoricCall>();
                List<HistoricCall> otherObstaclesInBatch = new List<HistoricCall>();
                float distanceForInsertedNotes = callsToCorrect[0].callDistance;
                foreach (HistoricCall callToCorrect in callsToCorrect)
                {
                    if (moveEarlier || moveLater)
                    {
                        // the distance corrections should all be the same so any inserted notes will have to use this distance instead
                        distanceForInsertedNotes = CreateDistanceCorrection(callToCorrect, moveEarlier, moveLater, rawVoiceMessage);
                    }
                    if (IsCorner(callToCorrect.callType))
                    {
                        cornersInBatch.Add(callToCorrect);
                    }
                    else if (IsTightens(callToCorrect.callType))
                    {
                        tightensInBatch.Add(callToCorrect);
                    }
                    else
                    {
                        otherObstaclesInBatch.Add(callToCorrect);
                    }
                }
                // if we're making a corner correction but haven't included a direction, we'll need to add it - use the last corner in the batch
                if (requestedDirection == Direction.UNKNOWN && cornersInBatch.Count > 0)
                {
                    // if there's a corner in the batch use the last corner's direction to see if we need to include a direction
                    String assumedDirection = GetDirectionFromPaceNote(cornersInBatch[cornersInBatch.Count - 1].callType) == Direction.LEFT ?
                        SpeechRecogniser.RALLY_LEFT[0] : SpeechRecogniser.RALLY_RIGHT[0];
                    foreach (string correctionText in SpeechRecogniser.RALLY_CORRECTION)
                    {
                        voiceMessage = voiceMessage.Replace(correctionText, assumedDirection);
                    }
                }
                ApplyPaceNoteCorrections(trackName, voiceMessage, rawVoiceMessage, distanceForInsertedNotes, moveEarlier || moveLater,
                    cornersInBatch, tightensInBatch, otherObstaclesInBatch);
            }
        }

        private void ApplyPaceNoteCorrections(string trackName, string voiceMessage, string rawVoiceMessage, float distanceForInsertedNotes, bool movedNotes,
            List<HistoricCall> cornersInBatch, List<HistoricCall> tightensInBatch, List<HistoricCall> otherObstaclesInBatch)
        {
            List<CoDriverPacenote> corrections = GetPacenotesFromVoiceCommand(voiceMessage, false, true);
            bool appliedTightensCorrection = false;
            bool appliedCornerCorrection = false;
            bool appliedObstacleCorrection = false;
            bool appliedModifierCorrection = false;
            foreach (CoDriverPacenote correction in corrections)
            {
                bool isCorner = IsCorner(correction.Pacenote);
                bool isTightens = IsTightens(correction.Pacenote);
                bool isJustModifier = correction.Modifier != PacenoteModifier.none && correction.Pacenote == PacenoteType.unknown;
                if (!appliedTightensCorrection && isTightens)
                {
                    if (tightensInBatch.Count == 0)
                    {
                        // we've made a "tightens to..." correction but there's no existing tightens so if we have a
                        // corner in the batch add this as an 'appends'
                        if (cornersInBatch.Count > 0)
                        {
                            if (cornersInBatch.Count != 1)
                            {
                                // we have "tightens to..." in the correction but more than 1 corner in the batch, apply the correction to the
                                // last one but log a warning
                                Console.WriteLine("Correction " + correction.ToString() + " may apply to one of " + cornersInBatch.Count + " corners, assuming it's the last one");
                            }
                            CreateObstacleOrCornerCorrection(cornersInBatch[cornersInBatch.Count - 1], correction.Pacenote, PacenoteModifier.none, rawVoiceMessage, true);
                        }
                        appliedTightensCorrection = true;
                    }
                    else
                    {
                        // if we have a tightens correction and one or more tightens calls in the batch, update the last one
                        CreateObstacleOrCornerCorrection(tightensInBatch[tightensInBatch.Count - 1], correction.Pacenote, PacenoteModifier.none, rawVoiceMessage, false);
                        if (tightensInBatch.Count > 1)
                        {
                            Console.WriteLine("Warning: Correction " + correction.ToString() + " could apply to multiple notes, applying it to last in batch");
                        }
                        appliedTightensCorrection = true;
                    }
                }
                if (!appliedCornerCorrection && isCorner)
                {
                    appliedCornerCorrection = true;
                    appliedModifierCorrection = true;
                    if (cornersInBatch.Count == 0)
                    {
                        // we've made a corner correction but there's no existing corner in the batch so add this as an insert
                        correctionsForCurrentSession.Add(new CoDriverPacenote
                        {
                            Distance = distanceForInsertedNotes,
                            CorrectedDistance = distanceForInsertedNotes,
                            Pacenote = correction.Pacenote,
                            CorrectedPacenoteType = correction.CorrectedPacenoteType,
                            Modifier = correction.Modifier,
                            CorrectedPacenoteModifier = correction.Modifier
                        });
                    }
                    else
                    {
                        // if we have a corner correction and at least one corner call in the batch, update it - use the last in the batch if there's more than one
                        CreateObstacleOrCornerCorrection(cornersInBatch[cornersInBatch.Count - 1], correction.Pacenote,
                            GetCorrectedModifier(cornersInBatch[cornersInBatch.Count - 1].modifier, correction.Modifier), rawVoiceMessage, false);
                        if (cornersInBatch.Count > 1)
                        {
                            Console.WriteLine("Warning: Correction " + correction.ToString() + " could apply to multiple notes, applying it to last in batch");
                        }
                    }
                }
                if (!appliedObstacleCorrection && !isCorner && !isTightens && !isJustModifier)
                {
                    appliedObstacleCorrection = true;
                    appliedModifierCorrection = true;
                    // we've no idea which obstacle might need correcting or inserting here. If there's a single obstacle, correct it otherwise
                    // insert a new note
                    if (otherObstaclesInBatch.Count == 1)
                    {
                        // if we have an obstacle correction and a single obstacle call in the batch, update it
                        CreateObstacleOrCornerCorrection(otherObstaclesInBatch[0], correction.Pacenote,
                            GetCorrectedModifier(otherObstaclesInBatch[0].modifier, correction.Modifier), rawVoiceMessage, false);
                    }
                    else
                    {
                        // we've made an obstacle correction but there are zero or many existing obstacles in the batch so add this as an insert
                        correctionsForCurrentSession.Add(new CoDriverPacenote
                        {
                            Distance = distanceForInsertedNotes,
                            CorrectedDistance = distanceForInsertedNotes,
                            Pacenote = correction.Pacenote,
                            CorrectedPacenoteType = correction.CorrectedPacenoteType,
                            Modifier = correction.Modifier,
                            CorrectedPacenoteModifier = correction.Modifier
                        });
                    }
                }
                if (!appliedModifierCorrection && isJustModifier)
                {
                    // we don't know which of the batch elements to apply the modifier to. Prefer corners
                    if (cornersInBatch.Count > 0)
                    {
                        appliedModifierCorrection = true;
                        CreateObstacleOrCornerCorrection(cornersInBatch[cornersInBatch.Count - 1], cornersInBatch[cornersInBatch.Count - 1].callType,
                            GetCorrectedModifier(cornersInBatch[cornersInBatch.Count - 1].modifier, correction.Modifier), rawVoiceMessage, false);
                    }
                    else if (otherObstaclesInBatch.Count > 0)
                    {
                        appliedModifierCorrection = true;
                        CreateObstacleOrCornerCorrection(otherObstaclesInBatch[otherObstaclesInBatch.Count - 1], otherObstaclesInBatch[otherObstaclesInBatch.Count - 1].callType,
                            GetCorrectedModifier(otherObstaclesInBatch[otherObstaclesInBatch.Count - 1].modifier, correction.Modifier), rawVoiceMessage, false);
                    }
                }
            }
            if (movedNotes || appliedCornerCorrection || appliedTightensCorrection || appliedObstacleCorrection || appliedModifierCorrection)
            {
                this.audioPlayer.playMessageImmediately(new QueuedMessage(this.folderAcknowlegeOK, 0));
                WritePacenoteCorrections(trackName);
            }
        }

        private void CreateObstacleOrCornerCorrection(HistoricCall callToCorrect, PacenoteType pacenoteType, PacenoteModifier pacenoteModifier, string rawVoiceMessage, bool appendToExisting)
        {
            if (appendToExisting)
            {
                Console.WriteLine("Extending existing pace note " + callToCorrect.ToString() + " at distance " + callToCorrect.callDistance + " with " + pacenoteType + ":" + pacenoteModifier);
            }
            else
            {
                Console.WriteLine("Correcting existing pace note " + callToCorrect.ToString() + " at distance " + callToCorrect.callDistance + " to be " + pacenoteType + ":" + pacenoteModifier);
            }

            CoDriverPacenote existingCorrection = FindMatchingCorrection(callToCorrect.callType, callToCorrect.modifier, callToCorrect.callDistance);
            if (existingCorrection != null)
            {
                if (pacenoteType != PacenoteType.unknown)
                {
                    existingCorrection.CorrectedPacenoteType = pacenoteType;
                }
                if (pacenoteModifier != PacenoteModifier.none)
                {
                    existingCorrection.CorrectedPacenoteModifier = pacenoteModifier;
                }
                existingCorrection.RawVoiceCommand = rawVoiceMessage;
            }
            else
            {
                CoDriverPacenote correctedPacenote = new CoDriverPacenote()
                {
                    Distance = callToCorrect.callDistance, /* the original distance */
                    Pacenote = callToCorrect.callType,     /* the original call type*/
                    Modifier = callToCorrect.modifier,     /* the original modifier */
                    RawVoiceCommand = rawVoiceMessage
                };
                if (pacenoteType != PacenoteType.unknown)
                {
                    correctedPacenote.CorrectedPacenoteType = pacenoteType; /* the corrected call type */
                }
                if (pacenoteModifier != PacenoteModifier.none)
                {
                    correctedPacenote.CorrectedPacenoteModifier = pacenoteModifier; /* the corrected modifier */
                }
                // if this is an appends, set the flag otherwise leave it as null
                if (appendToExisting)
                {
                    correctedPacenote.AppendToNote = true;
                }
                correctionsForCurrentSession.Add(correctedPacenote);
            }
        }

        private float CreateDistanceCorrection(HistoricCall callToCorrect, bool moveEarlier, bool moveLater, string rawVoiceMessage)
        {
            Console.WriteLine("Correcting existing pace note " + callToCorrect.ToString() + " at distance " + callToCorrect.callDistance + " to be " + (moveEarlier ? "earlier" : "later"));
            CoDriverPacenote existingCorrection = FindMatchingCorrection(callToCorrect.callType, callToCorrect.modifier, callToCorrect.callDistance);
            float correction = moveEarlier ? -50 : 50;
            if (existingCorrection != null)
            {
                correction += existingCorrection.GetDistance();
                existingCorrection.CorrectedDistance = correction;
                existingCorrection.RawVoiceCommand = rawVoiceMessage;
            }
            else
            {
                correction += callToCorrect.callDistance;
                correctionsForCurrentSession.Add(new CoDriverPacenote()
                {
                    Pacenote = callToCorrect.callType,     /* the original call type*/
                    Modifier = callToCorrect.modifier,     /* the original modifier */
                    Distance = callToCorrect.callDistance, /* the original distance */
                    CorrectedDistance = correction,
                    RawVoiceCommand = rawVoiceMessage
                });
            }
            return correction;
        }

        private CoDriverPacenote FindMatchingCorrection(PacenoteType pacenote, PacenoteModifier modifier, float distance)
        {
            foreach (CoDriverPacenote paceNoteCorrection in this.correctionsForCurrentSession)
            {
                // find a correction which matches the original pace note
                if (Math.Abs(paceNoteCorrection.Distance - distance) < 1 && paceNoteCorrection.Pacenote == pacenote && paceNoteCorrection.Modifier == modifier)
                {
                    return paceNoteCorrection;
                }
            }
            return null;
        }

        private void WritePacenoteCorrections(string trackName)
        {
            string pacenotesPath = GetPacenotesPath(trackName, false);
            Directory.CreateDirectory(pacenotesPath);
            File.WriteAllText(Path.Combine(pacenotesPath, CoDriver.correctionsFileName), JsonConvert.SerializeObject(this.correctionsForCurrentSession, Formatting.Indented));
        }

        private List<HistoricCall> GetCallsToCorrect(Direction requestedDirection)
        {
            // Count back through the historic calls to find the first one that we've passed and that matches the corner direction (if we specified one).
            // If we find a call to correct and it's for a corner > 400 metres behind us, assume we've not been able to find the appropriate call
            float lastNodeDistance = -1f;
            PacenoteType lastNodePacenoteType = PacenoteType.unknown;
            PacenoteModifier lastNodeModifier = PacenoteModifier.none;
            var historicCallNode = this.historicCalls.Last;
            float distance = MainWindow.voiceOption == MainWindow.VoiceOptionEnum.ALWAYS_ON ?
                    CrewChief.currentGameState.PositionAndMotionData.DistanceRoundTrack : SpeechRecogniser.distanceWhenVoiceCommandStarted;
            while (historicCallNode != null
                && (historicCallNode.Value.callDistance > distance + 20   // we've not reached this pacenote
                    || (requestedDirection != Direction.UNKNOWN && requestedDirection != GetDirectionFromPaceNote(historicCallNode.Value.callType)))) // this pacenote's direction is incorrect
            {
                lastNodeDistance = historicCallNode.Value.callDistance;
                lastNodeModifier = historicCallNode.Value.modifier;
                lastNodePacenoteType = historicCallNode.Value.callType;
                historicCallNode = historicCallNode.Previous;
            }
            if (historicCallNode == null || distance - historicCallNode.Value.callDistance > 400 /* this pace note is for an obstacle / corner 400m behind us*/)
            {
                Console.WriteLine("Unable to find a pacenote to correct, current distance = " + distance + " last passed call distance = " + lastNodeDistance +
                    " type  = " + lastNodePacenoteType + " modifier = " + lastNodeModifier);
                return null;
            }

            // at this point we have the first historic call we want to correct, get this and the other calls made at the same distance#
            // which have been played before this call. Note that if we have "caution, left 3, right 4" in the same batch and the correction
            // is "correction, left 2" we'll only pass the "caution" and "left 3" back from this call. If the correction was just
            // "correction, 2" we'll pass "caution", "left 3" and "right 4" back, and the correction would apply to the "right 4"
            List<HistoricCall> historicCallsToCorrect = new List<HistoricCall>();
            float distanceOfFirstCorrectedCall = historicCallNode.Value.callDistance;
            historicCallsToCorrect.Add(historicCallNode.Value);
            var preceedingCallNode = historicCallNode.Previous;
            while (preceedingCallNode != null)
            {
                if (preceedingCallNode.Value.callDistance == distanceOfFirstCorrectedCall)
                {
                    historicCallsToCorrect.Add(preceedingCallNode.Value);
                    preceedingCallNode = preceedingCallNode.Previous;
                }
                else
                {
                    break;
                }
            }
            // at this point the historicCalls are ordered such that the most recently played call is at the front of the list.
            // To make the calling code a bit easier to follow, reverse this so the historic calls are in a more natural order
            historicCallsToCorrect.Reverse();
            return historicCallsToCorrect;
        }

        private Direction GetDirectionFromPaceNote(PacenoteType paceNote)
        {
            if (paceNote.ToString().ToLower().Contains("left"))
                return Direction.LEFT;
            else if (paceNote.ToString().ToLower().Contains("right"))
                return Direction.RIGHT;
            else
                return Direction.UNKNOWN;
        }

        private List<VoiceMessagePaceNoteResult> GetCornerPacenoteTypesWithModifiers(MutableString voiceMessageWrapper, bool allowModifierOnly)
        {
            List<VoiceMessagePaceNoteResult> results = new List<VoiceMessagePaceNoteResult>();
            bool gotCornerType = false;

            List<VoiceMessagePaceNoteResult> tightensCalls = new List<VoiceMessagePaceNoteResult>();
            foreach (string key in possibleTightensCommands.Keys)
            {
                if (voiceMessageWrapper.FindAndRemove(key, false, true, voiceMessageWrapper.GetLength(), out int tightensStartPoint))
                {
                    gotCornerType = true;
                    tightensCalls.Add(new VoiceMessagePaceNoteResult(possibleTightensCommands[key], tightensStartPoint));
                }
            }
            voiceMessageWrapper.ResetCursor();
            foreach (string key in possibleCornerCommands.Keys)
            {
                int startPoint = -1;
                if (voiceMessageWrapper.FindAndRemove(key, false, true, voiceMessageWrapper.GetLength(), out startPoint))
                {
                    results.Add(new VoiceMessagePaceNoteResult((PacenoteType)possibleCornerCommands[key], startPoint));
                    gotCornerType = true;
                }
            }

            // now add the tightens if we found any
            results.AddRange(tightensCalls);

            // now find the modifiers and apply them to the correct calls
            voiceMessageWrapper.ResetCursor();
            for (int i = 0; i < results.Count; i++)
            {
                int startOfNextMatch = i == results.Count - 1 ? voiceMessageWrapper.GetLength() : results[i + 1].matchStartPoint;
                results[i].pacenoteModifier = GetModifier(voiceMessageWrapper, startOfNextMatch, out int startPointOfLastModifier);
            }

            if (!gotCornerType && allowModifierOnly)
            {
                PacenoteModifier modifier = GetModifier(voiceMessageWrapper, voiceMessageWrapper.GetLength(), out int startPoint);
                results.Add(new VoiceMessagePaceNoteResult(PacenoteType.unknown, modifier, startPoint));
            }
            return results.OrderBy(result => result.matchStartPoint).ToList();
        }

        private List<VoiceMessagePaceNoteResult> GetObstaclePacenoteTypesWithModifiers(MutableString voiceMessageWrapper)
        {
            List<VoiceMessagePaceNoteResult> matches = new List<VoiceMessagePaceNoteResult>();
            foreach (string[] command in SpeechRecogniser.RallyObstacleCommands)
            {
                int startPoint = -1;
                if (voiceMessageWrapper.FindAndRemove(command, false, true, voiceMessageWrapper.GetLength(), out startPoint))
                {
                    VoiceMessagePaceNoteResult result = new VoiceMessagePaceNoteResult(obstaclePacenoteTypes[command], startPoint);
                    // don't re-add this if it already contains it. The equals method on the result object ignores the startPoint, so this
                    // prevents us replacing the hit with one later in the command
                    if (!matches.Contains(result))
                    {
                        matches.Add(result);
                    }
                }
            }
            // now find the modifiers and apply them to the correct calls
            voiceMessageWrapper.ResetCursor();
            for (int i = 0; i < matches.Count; i++)
            {
                int startOfNextMatch = i == matches.Count - 1 ? voiceMessageWrapper.GetLength() : matches[i + 1].matchStartPoint;
                matches[i].pacenoteModifier = GetModifier(voiceMessageWrapper, startOfNextMatch, out int startPointOfLastModifier);
            }
            // at this point the list of obstacles will match the ordering of SpeechRecogniser.RallyObstacleCommands. Reorder it to
            // match the voice command order:
            return matches.OrderBy(item => item.matchStartPoint).ToList();
        }

        // this is quite specific - we only want modifiers that come after the position in the string
        // where we got our previous command
        private PacenoteModifier GetModifier(MutableString voiceMessageWrapper, int searchEndIndex, out int startPointOfModifier)
        {
            // note that startPointOfModifier will be set to the start point of whichever modifier the code happens to
            // match last. While this isn't important at the time of writing, it's a handy land-mine to ruin an otherwise
            // productive day in some unimagined future.
            startPointOfModifier = -1;
            PacenoteModifier modifier = PacenoteModifier.none;
            bool allowCut = true;
            if (voiceMessageWrapper.FindAndRemove(SpeechRecogniser.RALLY_TIGHTENS_BAD, true, false, searchEndIndex, out startPointOfModifier))
            {
                modifier = modifier | PacenoteModifier.detail_double_tightens;
            }
            if (!voiceMessageWrapper.ContainsAny(SpeechRecogniser.RALLY_TIGHTENS_THEN_OPENS, true)
                && !voiceMessageWrapper.ContainsAny(SpeechRecogniser.RALLY_OPENS_THEN_TIGHTENS, true)
                && voiceMessageWrapper.FindAndRemove(SpeechRecogniser.RALLY_TIGHTENS, true, false, searchEndIndex, out startPointOfModifier))
            {
                // additional check here - we don't want this to trigger for "tightens then opens" / "opens then tightens"
                modifier = modifier | PacenoteModifier.detail_tightens;
            }
            if (voiceMessageWrapper.FindAndRemove(SpeechRecogniser.RALLY_DONT_CUT, true, false, searchEndIndex, out startPointOfModifier))
            {
                allowCut = false;   // hack... cases where multiple notes are stacked in a single command can have cut and don't cut
                                    // This doesn't really work because the ordering is lost so we end up with "don't cut, cut"
                modifier = modifier | PacenoteModifier.detail_dont_cut;
            }
            if (allowCut && voiceMessageWrapper.FindAndRemove(SpeechRecogniser.RALLY_CUT, true, false, searchEndIndex, out startPointOfModifier))
            {
                modifier = modifier | PacenoteModifier.detail_cut;
            }
            if (!voiceMessageWrapper.ContainsAny(SpeechRecogniser.RALLY_TIGHTENS_THEN_OPENS, true)
                && !voiceMessageWrapper.ContainsAny(SpeechRecogniser.RALLY_OPENS_THEN_TIGHTENS, true)
                && voiceMessageWrapper.FindAndRemove(SpeechRecogniser.RALLY_OPENS, true, false, searchEndIndex, out startPointOfModifier))
            {
                // additional check here - we don't want this to trigger for "tightens then opens" / "opens then tightens"
                modifier = modifier | PacenoteModifier.detail_opens;
            }
            if (voiceMessageWrapper.FindAndRemove(SpeechRecogniser.RALLY_LONGLONG, true, false, searchEndIndex, out startPointOfModifier))
            {
                modifier = modifier | PacenoteModifier.detail_longlong;
            }
            else if (voiceMessageWrapper.FindAndRemove(SpeechRecogniser.RALLY_LONG, true, false, searchEndIndex, out startPointOfModifier)) /*long OR very long, not both*/
            {
                modifier = modifier | PacenoteModifier.detail_long;
            }
            if (voiceMessageWrapper.FindAndRemove(SpeechRecogniser.RALLY_WIDENS, true, false, searchEndIndex, out startPointOfModifier))
            {
                modifier = modifier | PacenoteModifier.detail_widens;
            }
            if (voiceMessageWrapper.FindAndRemove(SpeechRecogniser.RALLY_WIDE_OUT, true, false, searchEndIndex, out startPointOfModifier))
            {
                modifier = modifier | PacenoteModifier.detail_wideout;
            }
            if (voiceMessageWrapper.FindAndRemove(SpeechRecogniser.RALLY_MAYBE, true, false, searchEndIndex, out startPointOfModifier))
            {
                modifier = modifier | PacenoteModifier.detail_maybe;
            }
            if (voiceMessageWrapper.FindAndRemove(SpeechRecogniser.RALLY_PLUS, true, false, searchEndIndex, out startPointOfModifier))
            {
                modifier = modifier | PacenoteModifier.detail_plus;
            }
            else if (voiceMessageWrapper.FindAndRemove(SpeechRecogniser.RALLY_MINUS, true, false, searchEndIndex, out startPointOfModifier))
            {
                modifier = modifier | PacenoteModifier.detail_minus;
            }
            return modifier;
        }

        private bool IsCorner(PacenoteType pacenoteType)
        {
            return pacenoteType.ToString().Contains("corner");
        }

        private bool IsTightens(PacenoteType pacenoteType)
        {
            return pacenoteType == PacenoteType.detail_tightens_to_1
                || pacenoteType == PacenoteType.detail_tightens_to_2
                || pacenoteType == PacenoteType.detail_tightens_to_3
                || pacenoteType == PacenoteType.detail_tightens_to_4
                || pacenoteType == PacenoteType.detail_tightens_to_5
                || pacenoteType == PacenoteType.detail_tightens_to_acute;
        }


        private bool ContainsCornerModifier(string voiceMessage)
        {
            return SpeechRecogniser.ResultContains(voiceMessage, SpeechRecogniser.RALLY_DONT_CUT)
                || SpeechRecogniser.ResultContains(voiceMessage, SpeechRecogniser.RALLY_CUT)
                || SpeechRecogniser.ResultContains(voiceMessage, SpeechRecogniser.RALLY_TIGHTENS_BAD)
                || SpeechRecogniser.ResultContains(voiceMessage, SpeechRecogniser.RALLY_OPENS)
                || SpeechRecogniser.ResultContains(voiceMessage, SpeechRecogniser.RALLY_NARROWS)
                || SpeechRecogniser.ResultContains(voiceMessage, SpeechRecogniser.RALLY_MINUS)
                || SpeechRecogniser.ResultContains(voiceMessage, SpeechRecogniser.RALLY_PLUS)
                || SpeechRecogniser.ResultContains(voiceMessage, SpeechRecogniser.RALLY_MAYBE)
                || SpeechRecogniser.ResultContains(voiceMessage, SpeechRecogniser.RALLY_WIDENS)
                || SpeechRecogniser.ResultContains(voiceMessage, SpeechRecogniser.RALLY_LONG)
                || SpeechRecogniser.ResultContains(voiceMessage, SpeechRecogniser.RALLY_LONGLONG)
                || (SpeechRecogniser.ResultContains(voiceMessage, SpeechRecogniser.RALLY_TIGHTENS)
                    && !SpeechRecogniser.ResultContains(voiceMessage, SpeechRecogniser.RALLY_TIGHTENS_TO_1)
                    && !SpeechRecogniser.ResultContains(voiceMessage, SpeechRecogniser.RALLY_TIGHTENS_TO_2)
                    && !SpeechRecogniser.ResultContains(voiceMessage, SpeechRecogniser.RALLY_TIGHTENS_TO_3)
                    && !SpeechRecogniser.ResultContains(voiceMessage, SpeechRecogniser.RALLY_TIGHTENS_TO_4)
                    && !SpeechRecogniser.ResultContains(voiceMessage, SpeechRecogniser.RALLY_TIGHTENS_TO_5)
                    && !SpeechRecogniser.ResultContains(voiceMessage, SpeechRecogniser.RALLY_TIGHTENS_TO_HAIRPIN)
                    && !SpeechRecogniser.ResultContains(voiceMessage, SpeechRecogniser.RALLY_TIGHTENS_THEN_OPENS));
        }

        private PacenoteModifier GetCorrectedModifier(PacenoteModifier originalModifer, PacenoteModifier? correctionModifier)
        {
            if (correctionModifier == null)
            {
                return originalModifer;
            }
            else
            {
                PacenoteModifier newModifier = originalModifer;
                // start with the original modifier, strip off modifiers that have been negated by the correction, then add the correction
                if (originalModifer.HasFlag(PacenoteModifier.detail_cut) && correctionModifier.Value.HasFlag(PacenoteModifier.detail_dont_cut))
                {
                    // change from cut to don't cut
                    newModifier &= ~PacenoteModifier.detail_cut;
                }
                else if (originalModifer.HasFlag(PacenoteModifier.detail_dont_cut) && correctionModifier.Value.HasFlag(PacenoteModifier.detail_cut))
                {
                    // change from don't cut to cut
                    newModifier &= ~PacenoteModifier.detail_dont_cut;
                }
                if (originalModifer.HasFlag(PacenoteModifier.detail_widens) && correctionModifier.Value.HasFlag(PacenoteModifier.detail_narrows))
                {
                    // change from widens to narrows
                    newModifier &= ~PacenoteModifier.detail_widens;
                }
                else if (originalModifer.HasFlag(PacenoteModifier.detail_narrows) && correctionModifier.Value.HasFlag(PacenoteModifier.detail_widens))
                {
                    // change from narrows to widens
                    newModifier &= ~PacenoteModifier.detail_narrows;
                }
                if (originalModifer.HasFlag(PacenoteModifier.detail_minus) && correctionModifier.Value.HasFlag(PacenoteModifier.detail_plus))
                {
                    // change from minus to plus
                    newModifier &= ~PacenoteModifier.detail_minus;
                }
                else if (originalModifer.HasFlag(PacenoteModifier.detail_plus) && correctionModifier.Value.HasFlag(PacenoteModifier.detail_minus))
                {
                    // change from plus to minus
                    newModifier &= ~PacenoteModifier.detail_plus;
                }
                if (originalModifer.HasFlag(PacenoteModifier.detail_tightens)
                    && (correctionModifier.Value.HasFlag(PacenoteModifier.detail_double_tightens) || correctionModifier.Value.HasFlag(PacenoteModifier.detail_opens)))
                {
                    // change from tighens to double-tighens or opens
                    newModifier &= ~PacenoteModifier.detail_tightens;
                }
                else if (originalModifer.HasFlag(PacenoteModifier.detail_double_tightens)
                    && (correctionModifier.Value.HasFlag(PacenoteModifier.detail_tightens) || correctionModifier.Value.HasFlag(PacenoteModifier.detail_opens)))
                {
                    // change from double-tighens to tighens or opens
                    newModifier &= ~PacenoteModifier.detail_double_tightens;
                }
                else if (originalModifer.HasFlag(PacenoteModifier.detail_opens)
                        && (correctionModifier.Value.HasFlag(PacenoteModifier.detail_tightens) || correctionModifier.Value.HasFlag(PacenoteModifier.detail_double_tightens)))
                {
                    // change from opens to tightens or double-tightens
                    newModifier &= ~PacenoteModifier.detail_opens;
                }
                newModifier |= correctionModifier.Value;
                return newModifier;
            }
        }
    }

    class VoiceMessagePaceNoteResult
    {
        public CoDriver.PacenoteType pacenoteType;
        public CoDriver.PacenoteModifier pacenoteModifier;
        public int matchStartPoint;

        public VoiceMessagePaceNoteResult(CoDriver.PacenoteType pacenoteType, CoDriver.PacenoteModifier pacenoteModifier, int matchStartPoint)
        {
            this.pacenoteType = pacenoteType;
            this.pacenoteModifier = pacenoteModifier;
            this.matchStartPoint = matchStartPoint;
        }

        public VoiceMessagePaceNoteResult(CoDriver.PacenoteType pacenoteType, int matchStartPoint)
        {
            this.pacenoteType = pacenoteType;
            this.pacenoteModifier = CoDriver.PacenoteModifier.none;
            this.matchStartPoint = matchStartPoint;
        }

        // equals ignoring match location
        public override bool Equals(object other)
        {
            VoiceMessagePaceNoteResult otherResult = (VoiceMessagePaceNoteResult)other;
            return otherResult.pacenoteModifier == this.pacenoteModifier && otherResult.pacenoteType == this.pacenoteType;
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    class MutableString
    {
        string contents;
        int cursorPosition = 0;
        public MutableString(string contents)
        {
            this.contents = contents;
        }

        public string GetString()
        {
            return contents;
        }

        public int GetLength()
        {
            return contents.Length;
        }

        public void ResetCursor()
        {
            this.cursorPosition = 0;
        }

        // return true is the text is in our internal string, and remove it from the string.
        // If fromCursorPosition is true, we start the search from where our cursor is. If
        // updateCursor is true we move the cursor to the start of the located string.
        //
        // When looking for an obstacle or corner, we want to search the whole string and move the cursor
        // to the position after the match. Then we search for associated modifiers from the cursor position
        // but as there can be multiple modifiers, we keep the cursor at the same place so we can be sure to
        // find all the modifiers after the corner or obstacle.
        //
        public bool FindAndRemove(string text, bool fromCursorPosition, bool updateCursor, int searchEndIndex, out int startPoint)
        {
            if (fromCursorPosition && this.cursorPosition >= this.contents.Length)
            {
                startPoint = -1;
                return false;
            }

            searchEndIndex = Math.Min(searchEndIndex, this.contents.Length);
            int substringStartIndex = fromCursorPosition ? cursorPosition : 0;
            int substringLength = searchEndIndex - substringStartIndex;
            string substringToSearch = this.contents.Substring(substringStartIndex, substringLength);

            int matchIndex = substringToSearch.IndexOf(text);
            if (matchIndex != -1)
            {
                startPoint = fromCursorPosition ? this.cursorPosition + matchIndex : matchIndex;
                if (updateCursor)
                {
                    this.cursorPosition = startPoint;
                }
                this.contents = this.contents.Remove(startPoint, text.Length);
                this.contents = this.contents.Insert(startPoint, new string('x', text.Length));
                return true;
            }
            startPoint = -1;
            return false;
        }

        public bool FindAndRemove(string[] text, bool fromCursorPosition, bool updateCursorPosition, int searchEndIndex, out int startPoint)
        {
            foreach (string item in text)
            {
                if (FindAndRemove(item, fromCursorPosition, updateCursorPosition, searchEndIndex, out startPoint))
                {
                    return true;
                }
            }
            startPoint = -1;
            return false;
        }

        public bool ContainsAny(string[] text, bool fromCursorPosition)
        {
            if (fromCursorPosition && cursorPosition >= this.contents.Length)
            {
                return false;
            }
            foreach (string item in text)
            {
                int startIndex = fromCursorPosition ? this.cursorPosition : 0;
                if (this.contents.Substring(startIndex).Contains(item))
                {
                    return true;
                }
            }
            return false;
        }

        // get any text fragments that haven't been successfully processed, or null if all the message was processed.
        public string GetUnprocessedCommandText()
        {
            string uneatenText = this.contents.Replace("x", "").Trim();
            if (uneatenText.Length > 0)
            {
                Console.WriteLine("Voice command fragments left over: \"" + uneatenText + "\"");
                return uneatenText;
            }
            else
            {
                return null;
            }
        }
    }
}
