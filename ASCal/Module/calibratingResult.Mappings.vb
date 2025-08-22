' calibratingResult.Mappings.vb
Option Strict Off

Partial Class calibratingResult

    ' Centralized place for all hardcoded mappings so the main file stays short.
    ' Uses the existing MapTB / MapLBL / LockAutoFields helpers defined in the other partial.

    Private Sub InitMappings()

        ' ================== DC VOLTAGE ==================
        ' Inputs
        DC.MV1 = MapTB("X", 55, TextBox5, TextBox24, TextBox38, TextBox52, TextBox66,
                               TextBox136, TextBox122, TextBox108, TextBox94, TextBox80,
                               TextBox234, TextBox220, TextBox206, TextBox192, TextBox178,
                               TextBox164, TextBox150)
        DC.MV2 = MapTB("AC", 55, TextBox6, TextBox23, TextBox37, TextBox51, TextBox65,
                                TextBox135, TextBox121, TextBox107, TextBox93, TextBox79,
                                TextBox233, TextBox219, TextBox205, TextBox191, TextBox177,
                                TextBox163, TextBox149)
        DC.MV3 = MapTB("AH", 55, TextBox7, TextBox22, TextBox36, TextBox50, TextBox64,
                                TextBox134, TextBox120, TextBox106, TextBox92, TextBox78,
                                TextBox232, TextBox218, TextBox204, TextBox190, TextBox176,
                                TextBox162, TextBox148)

        ' Computed labels
        DC.Average = MapLBL("AM", 55, Label100, Label101, Label103, Label102, Label107,
                                         Label106, Label105, Label104, Label115, Label114,
                                         Label113, Label112, Label111, Label110, Label109,
                                         Label108, Label116)
        DC.Error = MapLBL("AR", 55, Label134, Label135, Label137, Label136, Label141,
                                         Label140, Label139, Label138, Label149, Label148,
                                         Label147, Label146, Label145, Label144, Label143,
                                         Label142, Label150)
        DC.FinalUncDecl = MapLBL("DD", 55, Label151, Label152, Label154, Label153, Label158,
                                         Label157, Label156, Label155, Label166, Label165,
                                         Label164, Label163, Label162, Label161, Label160,
                                         Label159, Label167)

        ' Mirrored outputs
        DC.Tolerance = MapTB("BB", 55, TextBox11, TextBox18, TextBox32, TextBox46, TextBox60,
                                        TextBox130, TextBox116, TextBox102, TextBox88, TextBox74,
                                        TextBox228, TextBox214, TextBox200, TextBox186, TextBox172,
                                        TextBox158, TextBox144)
        DC.UpperLimit = MapTB("BG", 55, TextBox14, TextBox17, TextBox31, TextBox45, TextBox59,
                                        TextBox129, TextBox115, TextBox101, TextBox87, TextBox73,
                                        TextBox227, TextBox213, TextBox199, TextBox185, TextBox171,
                                        TextBox157, TextBox143)
        DC.LowerLimit = MapTB("BL", 55, TextBox13, TextBox16, TextBox30, TextBox44, TextBox58,
                                        TextBox128, TextBox114, TextBox100, TextBox86, TextBox72,
                                        TextBox226, TextBox212, TextBox198, TextBox184, TextBox170,
                                        TextBox156, TextBox142)
        DC.Remarks = MapTB("BQ", 55, TextBox12, TextBox15, TextBox29, TextBox43, TextBox57,
                                        TextBox127, TextBox113, TextBox99, TextBox85, TextBox71,
                                        TextBox225, TextBox211, TextBox197, TextBox183, TextBox169,
                                        TextBox155, TextBox141)

        LockAutoFields(DC)   ' makes DC mirrored outputs read-only.

        ' ================== AC VOLTAGE ==================
        ' Inputs
        AC.MV1 = MapTB("X", 76, TextBox472, TextBox458, TextBox444, TextBox430, TextBox416, TextBox402, TextBox388, TextBox374,
                        TextBox360, TextBox346, TextBox332, TextBox318, TextBox304, TextBox290, TextBox276, TextBox262,
                        TextBox248, TextBox542, TextBox528, TextBox514, TextBox500, TextBox486)
        AC.MV2 = MapTB("AC", 76, TextBox471, TextBox457, TextBox443, TextBox429, TextBox415, TextBox401, TextBox387, TextBox373,
                        TextBox359, TextBox345, TextBox331, TextBox317, TextBox303, TextBox289, TextBox275, TextBox261,
                        TextBox247, TextBox541, TextBox527, TextBox513, TextBox499, TextBox485)
        AC.MV3 = MapTB("AH", 76, TextBox470, TextBox456, TextBox442, TextBox428, TextBox414, TextBox400, TextBox386, TextBox372,
                        TextBox358, TextBox344, TextBox330, TextBox316, TextBox302, TextBox288, TextBox274, TextBox260,
                        TextBox246, TextBox540, TextBox526, TextBox512, TextBox498, TextBox484)

        ' Computed labels
        AC.Average = MapLBL("AM", 76, Label352, Label351, Label350, Label349, Label348, Label347, Label346, Label345, Label344,
                                  Label343, Label342, Label341, Label340, Label339, Label338, Label337, Label336, Label367,
                                  Label366, Label365, Label364, Label363)
        AC.Error = MapLBL("AR", 76, Label335, Label334, Label333, Label332, Label331, Label330, Label329, Label328, Label327,
                                  Label326, Label325, Label324, Label323, Label322, Label321, Label320, Label319, Label362,
                                  Label361, Label360, Label359, Label358)
        AC.FinalUncDecl = MapLBL("DD", 76, Label318, Label317, Label316, Label315, Label314, Label313, Label312, Label311, Label310,
                                  Label309, Label308, Label307, Label306, Label305, Label304, Label303, Label296, Label357,
                                  Label356, Label355, Label354, Label353)

        ' Mirrored outputs
        AC.Tolerance = MapTB("BB", 76, TextBox466, TextBox452, TextBox438, TextBox424, TextBox410, TextBox396, TextBox382, TextBox368,
                               TextBox354, TextBox340, TextBox326, TextBox312, TextBox298, TextBox284, TextBox270, TextBox256,
                               TextBox242, TextBox536, TextBox522, TextBox508, TextBox494, TextBox480)
        AC.UpperLimit = MapTB("BG", 76, TextBox465, TextBox451, TextBox437, TextBox423, TextBox409, TextBox395, TextBox381, TextBox367,
                               TextBox353, TextBox339, TextBox325, TextBox311, TextBox297, TextBox283, TextBox269, TextBox255,
                               TextBox241, TextBox535, TextBox521, TextBox507, TextBox493, TextBox479)
        AC.LowerLimit = MapTB("BL", 76, TextBox464, TextBox450, TextBox436, TextBox422, TextBox408, TextBox394, TextBox380, TextBox366,
                               TextBox352, TextBox338, TextBox324, TextBox310, TextBox296, TextBox282, TextBox268, TextBox254,
                               TextBox240, TextBox534, TextBox520, TextBox506, TextBox492, TextBox478)
        AC.Remarks = MapTB("BQ", 76, TextBox463, TextBox449, TextBox435, TextBox421, TextBox407, TextBox393, TextBox379, TextBox365,
                               TextBox351, TextBox337, TextBox323, TextBox309, TextBox295, TextBox281, TextBox267, TextBox253,
                               TextBox239, TextBox533, TextBox519, TextBox505, TextBox491, TextBox477)

        LockAutoFields(AC)

        ' ================== RESISTANCE ==================
        ' Inputs
        RES.MV1 = MapTB("X", 102, TextBox780, TextBox766, TextBox752, TextBox738, TextBox724, TextBox710, TextBox696)
        RES.MV2 = MapTB("AC", 102, TextBox779, TextBox765, TextBox751, TextBox737, TextBox723, TextBox709, TextBox695)
        RES.MV3 = MapTB("AH", 102, TextBox778, TextBox764, TextBox750, TextBox736, TextBox722, TextBox708, TextBox694)

        ' Computed labels
        RES.Average = MapLBL("AM", 102, Label553, Label556, Label616, Label559, Label628, Label625, Label622)
        RES.Error = MapLBL("AR", 102, Label552, Label555, Label561, Label558, Label627, Label624, Label621)
        RES.FinalUncDecl = MapLBL("DD", 102, Label544, Label554, Label560, Label557, Label626, Label623, Label620)

        ' Mirrored outputs
        RES.Tolerance = MapTB("BB", 102, TextBox774, TextBox760, TextBox746, TextBox732, TextBox718, TextBox704, TextBox690)
        RES.UpperLimit = MapTB("BG", 102, TextBox773, TextBox759, TextBox745, TextBox731, TextBox717, TextBox703, TextBox689)
        RES.LowerLimit = MapTB("BL", 102, TextBox772, TextBox758, TextBox744, TextBox730, TextBox716, TextBox702, TextBox688)
        RES.Remarks = MapTB("BQ", 102, TextBox771, TextBox757, TextBox743, TextBox729, TextBox715, TextBox701, TextBox687)

        LockAutoFields(RES)

        ' ================== DC CURRENT ==================
        ' Inputs
        DCC.MV1 = MapTB("X", 113, TextBox126, TextBox112, TextBox98, TextBox84, TextBox70, TextBox56, TextBox42, TextBox28, TextBox10)
        DCC.MV2 = MapTB("AC", 113, TextBox125, TextBox111, TextBox97, TextBox83, TextBox69, TextBox55, TextBox41, TextBox27, TextBox9)
        DCC.MV3 = MapTB("AH", 113, TextBox124, TextBox110, TextBox96, TextBox82, TextBox68, TextBox54, TextBox40, TextBox26, TextBox8)

        ' Computed labels
        DCC.Average = MapLBL("AM", 113, Label403, Label402, Label401, Label400, Label390, Label389, Label388, Label387, Label386)
        DCC.Error = MapLBL("AR", 113, Label385, Label384, Label383, Label382, Label381, Label380, Label379, Label378, Label377)
        DCC.FinalUncDecl = MapLBL("DD", 113, Label376, Label375, Label374, Label373, Label372, Label371, Label370, Label369, Label368)

        ' Mirrored outputs
        DCC.Tolerance = MapTB("BB", 113, TextBox123, TextBox109, TextBox95, TextBox81, TextBox67, TextBox53, TextBox39, TextBox25, TextBox4)
        DCC.UpperLimit = MapTB("BG", 113, TextBox119, TextBox105, TextBox91, TextBox77, TextBox63, TextBox49, TextBox35, TextBox21, TextBox3)
        DCC.LowerLimit = MapTB("BL", 113, TextBox118, TextBox104, TextBox90, TextBox76, TextBox62, TextBox48, TextBox34, TextBox20, TextBox2)
        DCC.Remarks = MapTB("BQ", 113, TextBox117, TextBox103, TextBox89, TextBox75, TextBox61, TextBox47, TextBox33, TextBox19, TextBox1)

        LockAutoFields(DCC)

        ' keep DCC last row editable (shared with DC controls)
        TextBox12.ReadOnly = False   ' DCC.MV3 last-row input
        TextBox13.ReadOnly = False   ' DCC.MV2 last-row input
        ' (You already wire live compute on DCC.MV3; this lets row 9 complete.)

        ' ================== AC CURRENT ==================
        ' Inputs
        ACC.MV1 = MapTB("X", 126, TextBox308, TextBox294, TextBox280, TextBox266, TextBox252, TextBox238, TextBox224, TextBox210, TextBox196, TextBox168, TextBox154, TextBox140)
        ACC.MV2 = MapTB("AC", 126, TextBox307, TextBox293, TextBox279, TextBox265, TextBox251, TextBox237, TextBox223, TextBox209, TextBox195, TextBox167, TextBox153, TextBox139)
        ACC.MV3 = MapTB("AH", 126, TextBox306, TextBox292, TextBox278, TextBox264, TextBox250, TextBox236, TextBox222, TextBox208, TextBox194, TextBox166, TextBox152, TextBox138)

        ' Computed labels
        ACC.Average = MapLBL("AM", 126, Label433, Label432, Label431, Label430, Label429, Label428, Label427, Label426, Label425, Label421, Label420, Label419)
        ACC.Error = MapLBL("AR", 126, Label416, Label415, Label414, Label413, Label412, Label411, Label410, Label409, Label408, Label418, Label417, Label407)
        ACC.FinalUncDecl = MapLBL("DD", 126, Label399, Label398, Label397, Label396, Label395, Label394, Label393, Label392, Label391, Label406, Label405, Label404)

        ' Mirrored outputs
        ACC.Tolerance = MapTB("BB", 126, TextBox305, TextBox291, TextBox277, TextBox263, TextBox249, TextBox235, TextBox221, TextBox207, TextBox193, TextBox165, TextBox151, TextBox137)
        ACC.UpperLimit = MapTB("BG", 126, TextBox301, TextBox287, TextBox273, TextBox259, TextBox245, TextBox231, TextBox217, TextBox203, TextBox189, TextBox161, TextBox147, TextBox133)
        ACC.LowerLimit = MapTB("BL", 126, TextBox300, TextBox286, TextBox272, TextBox258, TextBox244, TextBox230, TextBox216, TextBox202, TextBox188, TextBox160, TextBox146, TextBox132)
        ACC.Remarks = MapTB("BQ", 126, TextBox299, TextBox285, TextBox271, TextBox257, TextBox243, TextBox229, TextBox215, TextBox201, TextBox187, TextBox159, TextBox145, TextBox131)

        LockAutoFields(ACC)

    End Sub

End Class