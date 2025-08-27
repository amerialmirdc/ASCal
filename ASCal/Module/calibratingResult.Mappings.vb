Option Strict Off
' =============================================================================
' calibratingResult.Mappings.vb — centralize control-to-Excel mappings
' =============================================================================

Partial Class calibratingResult

    Private Sub InitMappings()

        ' ===== Generated from mapping.xlsx (per row) =====

        ' --- DC VOLTAGE (17 rows: 55..71) ---
        DCV.RangeLbl = {L("Label49"), L("Label50"), L("Label51"), L("Label54"), L("Label53"), L("Label52"), L("Label60"), L("Label59"), L("Label58"), L("Label57"), L("Label56"), L("Label55"), L("Label66"), L("Label65"), L("Label64"), L("Label63"), L("Label62")}
        DCV.Unit1Lbl = {L("Label82"), L("Label81"), L("Label80"), L("Label79"), L("Label78"), L("Label77"), L("Label76"), L("Label75"), L("Label74"), L("Label73"), L("Label72"), L("Label71"), L("Label70"), L("Label69"), L("Label68"), L("Label67"), L("Label61")}
        DCV.NominalLbl = {L("Label99"), L("Label98"), L("Label97"), L("Label96"), L("Label95"), L("Label94"), L("Label93"), L("Label92"), L("Label91"), L("Label90"), L("Label89"), L("Label88"), L("Label87"), L("Label86"), L("Label85"), L("Label84"), L("Label83")}
        DCV.Unit2Lbl = {L("Label133"), L("Label132"), L("Label131"), L("Label130"), L("Label129"), L("Label128"), L("Label127"), L("Label126"), L("Label125"), L("Label124"), L("Label123"), L("Label122"), L("Label121"), L("Label120"), L("Label119"), L("Label118"), L("Label117")}
        ' New:
        DCV.FrequencyLbl = {}
        DCV.UnitLbl = {}

        DCV.MV1 = MapTB("X", 55, TextBox5, TextBox24, TextBox38, TextBox52, TextBox66, TextBox136, TextBox122, TextBox108, TextBox94, TextBox80, TextBox234, TextBox220, TextBox206, TextBox192, TextBox178, TextBox164, TextBox150)
        DCV.MV2 = MapTB("AC", 55, TextBox6, TextBox23, TextBox37, TextBox51, TextBox65, TextBox135, TextBox121, TextBox107, TextBox93, TextBox79, TextBox233, TextBox219, TextBox205, TextBox191, TextBox177, TextBox163, TextBox149)
        DCV.MV3 = MapTB("AH", 55, TextBox7, TextBox22, TextBox36, TextBox50, TextBox64, TextBox134, TextBox120, TextBox106, TextBox92, TextBox78, TextBox232, TextBox218, TextBox204, TextBox190, TextBox176, TextBox162, TextBox148)

        DCV.Average = MapLBL("AM", 55, Label100, Label101, Label103, Label102, Label107, Label106, Label105, Label104, Label115, Label114, Label113, Label112, Label111, Label110, Label109, Label108, Label116)
        DCV.Error = MapLBL("AR", 55, Label134, Label135, Label137, Label136, Label141, Label140, Label139, Label138, Label149, Label148, Label147, Label146, Label145, Label144, Label143, Label142, Label150)
        DCV.FinalUncDecl = MapLBL("DD", 55, Label151, Label152, Label154, Label153, Label158, Label157, Label156, Label155, Label166, Label165, Label164, Label163, Label162, Label161, Label160, Label159, Label167)

        DCV.Tolerance = MapTB("BB", 55, TextBox11, TextBox18, TextBox32, TextBox46, TextBox60, TextBox130, TextBox116, TextBox102, TextBox88, TextBox74, TextBox228, TextBox214, TextBox200, TextBox186, TextBox172, TextBox158, TextBox144)
        DCV.UpperLimit = MapTB("BG", 55, TextBox14, TextBox17, TextBox31, TextBox45, TextBox59, TextBox129, TextBox115, TextBox101, TextBox87, TextBox73, TextBox227, TextBox213, TextBox199, TextBox185, TextBox171, TextBox157, TextBox143)
        DCV.LowerLimit = MapTB("BL", 55, TextBox13, TextBox16, TextBox30, TextBox44, TextBox58, TextBox128, TextBox114, TextBox100, TextBox86, TextBox72, TextBox226, TextBox212, TextBox198, TextBox184, TextBox170, TextBox156, TextBox142)
        DCV.Remarks = MapTB("BQ", 55, TextBox12, TextBox15, TextBox29, TextBox43, TextBox57, TextBox127, TextBox113, TextBox99, TextBox85, TextBox71, TextBox225, TextBox211, TextBox197, TextBox183, TextBox169, TextBox155, TextBox141)

        LockAutoFields(DCV)

        ' --- AC VOLTAGE (22 rows: 76..97) ---
        ACV.RangeLbl = {L("Label201"), L("Label200"), L("Label199"), L("Label203"), L("Label198"), L("Label197"), L("Label196"), L("Label205"), L("Label195"), L("Label194"), L("Label193"), L("Label192"), L("Label191"), L("Label207"), L("Label190"), L("Label189"), L("Label188"), L("Label209"), L("Label187"), L("Label186"), L("Label185"), L("Label211")}
        ACV.Unit1Lbl = {L("Label184"), L("Label183"), L("Label182"), L("Label202"), L("Label181"), L("Label180"), L("Label179"), L("Label204"), L("Label178"), L("Label177"), L("Label176"), L("Label175"), L("Label174"), L("Label206"), L("Label173"), L("Label172"), L("Label171"), L("Label208"), L("Label170"), L("Label169"), L("Label168"), L("Label210")}
        ACV.NominalLbl = {L("Label245"), L("Label256"), L("Label244"), L("Label243"), L("Label254"), L("Label242"), L("Label241"), L("Label240"), L("Label239"), L("Label252"), L("Label236"), L("Label238"), L("Label237"), L("Label235"), L("Label234"), L("Label250"), L("Label233"), L("Label232"), L("Label231"), L("Label248"), L("Label230"), L("Label229")}
        ACV.Unit2Lbl = {L("Label228"), L("Label255"), L("Label227"), L("Label226"), L("Label253"), L("Label225"), L("Label224"), L("Label223"), L("Label222"), L("Label251"), L("Label219"), L("Label221"), L("Label220"), L("Label218"), L("Label217"), L("Label249"), L("Label216"), L("Label215"), L("Label214"), L("Label29"), L("Label213"), L("Label212")}
        ' New:
        ACV.FrequencyLbl = {L("Label258"), L("Label257"), L("Label260"), L("Label259"), L("Label264"), L("Label263"), L("Label262"), L("Label261"), L("Label272"), L("Label271"), L("Label270"), L("Label269"), L("Label268"), L("Label267"), L("Label266"), L("Label265"), L("Label278"), L("Label277"), L("Label276"), L("Label275"), L("Label274"), L("Label273")}
        ACV.UnitLbl = {L("Label280"), L("Label279"), L("Label282"), L("Label281"), L("Label286"), L("Label285"), L("Label284"), L("Label283"), L("Label294"), L("Label293"), L("Label292"), L("Label291"), L("Label290"), L("Label289"), L("Label288"), L("Label287"), L("Label302"), L("Label301"), L("Label300"), L("Label299"), L("Label298"), L("Label297")}

        ACV.MV1 = MapTB("X", 76, TextBox472, TextBox458, TextBox444, TextBox430, TextBox416, TextBox402, TextBox388, TextBox374, TextBox360, TextBox346, TextBox332, TextBox318, TextBox304, TextBox290, TextBox276, TextBox262, TextBox248, TextBox542, TextBox528, TextBox514, TextBox500, TextBox486)
        ACV.MV2 = MapTB("AC", 76, TextBox471, TextBox457, TextBox443, TextBox429, TextBox415, TextBox401, TextBox387, TextBox373, TextBox359, TextBox345, TextBox331, TextBox317, TextBox303, TextBox289, TextBox275, TextBox261, TextBox247, TextBox541, TextBox527, TextBox513, TextBox499, TextBox485)
        ACV.MV3 = MapTB("AH", 76, TextBox470, TextBox456, TextBox442, TextBox428, TextBox414, TextBox400, TextBox386, TextBox372, TextBox358, TextBox344, TextBox330, TextBox316, TextBox302, TextBox288, TextBox274, TextBox260, TextBox246, TextBox540, TextBox526, TextBox512, TextBox498, TextBox484)

        ACV.Average = MapLBL("AM", 76, Label352, Label351, Label350, Label349, Label348, Label347, Label346, Label345, Label344, Label343, Label342, Label341, Label340, Label339, Label338, Label337, Label336, Label367, Label366, Label365, Label364, Label363)
        ACV.Error = MapLBL("AR", 76, Label335, Label334, Label333, Label332, Label331, Label330, Label329, Label328, Label327, Label326, Label325, Label324, Label323, Label322, Label321, Label320, Label319, Label362, Label361, Label360, Label359, Label358)
        ACV.FinalUncDecl = MapLBL("DD", 76, Label318, Label317, Label316, Label315, Label314, Label313, Label312, Label311, Label310, Label309, Label308, Label307, Label306, Label305, Label304, Label303, Label296, Label357, Label356, Label355, Label354, Label353)

        ACV.Tolerance = MapTB("BB", 76, TextBox466, TextBox452, TextBox438, TextBox424, TextBox410, TextBox396, TextBox382, TextBox368, TextBox354, TextBox340, TextBox326, TextBox312, TextBox298, TextBox284, TextBox270, TextBox256, TextBox242, TextBox536, TextBox522, TextBox508, TextBox494, TextBox480)
        ACV.UpperLimit = MapTB("BG", 76, TextBox465, TextBox451, TextBox437, TextBox423, TextBox409, TextBox395, TextBox381, TextBox367, TextBox353, TextBox339, TextBox325, TextBox311, TextBox297, TextBox283, TextBox269, TextBox255, TextBox241, TextBox535, TextBox521, TextBox507, TextBox493, TextBox479)
        ACV.LowerLimit = MapTB("BL", 76, TextBox464, TextBox450, TextBox436, TextBox422, TextBox408, TextBox394, TextBox380, TextBox366, TextBox352, TextBox338, TextBox324, TextBox310, TextBox296, TextBox282, TextBox268, TextBox254, TextBox240, TextBox534, TextBox520, TextBox506, TextBox492, TextBox478)
        ACV.Remarks = MapTB("BQ", 76, TextBox463, TextBox449, TextBox435, TextBox421, TextBox407, TextBox393, TextBox379, TextBox365, TextBox351, TextBox337, TextBox323, TextBox309, TextBox295, TextBox281, TextBox267, TextBox253, TextBox239, TextBox533, TextBox519, TextBox505, TextBox491, TextBox477)

        LockAutoFields(ACV)

        ' --- RESISTANCE (7 rows: 102..108) ---
        RES.RangeLbl = {L("Label584"), L("Label587"), L("Label589"), L("Label595"), L("Label593"), L("Label591"), L("Label597")}
        RES.Unit1Lbl = {L("Label585"), L("Label586"), L("Label588"), L("Label594"), L("Label592"), L("Label590"), L("Label596")}
        RES.NominalLbl = {L("Label611"), L("Label609"), L("Label607"), L("Label605"), L("Label603"), L("Label601"), L("Label599")}
        RES.Unit2Lbl = {L("Label610"), L("Label608"), L("Label606"), L("Label604"), L("Label602"), L("Label600"), L("Label598")}
        ' New:
        RES.FrequencyLbl = {}
        RES.UnitLbl = {}

        RES.MV1 = MapTB("X", 102, TextBox780, TextBox766, TextBox752, TextBox738, TextBox724, TextBox710, TextBox696)
        RES.MV2 = MapTB("AC", 102, TextBox779, TextBox765, TextBox751, TextBox737, TextBox723, TextBox709, TextBox695)
        RES.MV3 = MapTB("AH", 102, TextBox778, TextBox764, TextBox750, TextBox736, TextBox722, TextBox708, TextBox694)

        RES.Average = MapLBL("AM", 102, Label553, Label556, Label616, Label559, Label628, Label625, Label622)
        RES.Error = MapLBL("AR", 102, Label552, Label555, Label561, Label558, Label627, Label624, Label621)
        RES.FinalUncDecl = MapLBL("DD", 102, Label544, Label554, Label560, Label557, Label626, Label623, Label620)

        RES.Tolerance = MapTB("BB", 102, TextBox774, TextBox760, TextBox746, TextBox732, TextBox718, TextBox704, TextBox690)
        RES.UpperLimit = MapTB("BG", 102, TextBox773, TextBox759, TextBox745, TextBox731, TextBox717, TextBox703, TextBox689)
        RES.LowerLimit = MapTB("BL", 102, TextBox772, TextBox758, TextBox744, TextBox730, TextBox716, TextBox702, TextBox688)
        RES.Remarks = MapTB("BQ", 102, TextBox771, TextBox757, TextBox743, TextBox729, TextBox715, TextBox701, TextBox687)

        LockAutoFields(RES)

        ' --- DC CURRENT (9 rows: 113..121) ---
        DCC.RangeLbl = {L("Label508"), L("Label507"), L("Label499"), L("Label487"), L("Label498"), L("Label497"), L("Label496"), L("Label485"), L("Label495")}
        DCC.Unit1Lbl = {L("Label494"), L("Label493"), L("Label492"), L("Label486"), L("Label491"), L("Label490"), L("Label489"), L("Label484"), L("Label488")}
        DCC.NominalLbl = {L("Label468"), L("Label443"), L("Label467"), L("Label466"), L("Label445"), L("Label465"), L("Label464"), L("Label463"), L("Label462")}
        DCC.Unit2Lbl = {L("Label461"), L("Label460"), L("Label459"), L("Label444"), L("Label458"), L("Label457"), L("Label446"), L("Label442"), L("Label446")}
        ' New:
        DCC.FrequencyLbl = {}
        DCC.UnitLbl = {}

        DCC.MV1 = MapTB("X", 113, TextBox126, TextBox112, TextBox98, TextBox84, TextBox70, TextBox56, TextBox42, TextBox28, TextBox10)
        DCC.MV2 = MapTB("AC", 113, TextBox125, TextBox111, TextBox97, TextBox83, TextBox69, TextBox55, TextBox41, TextBox27, TextBox9)
        DCC.MV3 = MapTB("AH", 113, TextBox124, TextBox110, TextBox96, TextBox82, TextBox68, TextBox54, TextBox40, TextBox26, TextBox8)

        DCC.Average = MapLBL("AM", 113, Label403, Label402, Label401, Label400, Label390, Label389, Label388, Label387, Label386)
        DCC.Error = MapLBL("AR", 113, Label385, Label384, Label383, Label382, Label381, Label380, Label379, Label378, Label377)
        DCC.FinalUncDecl = MapLBL("DD", 113, Label376, Label375, Label374, Label373, Label372, Label371, Label370, Label369, Label368)

        DCC.Tolerance = MapTB("BB", 113, TextBox123, TextBox109, TextBox95, TextBox81, TextBox67, TextBox53, TextBox39, TextBox25, TextBox4)
        DCC.UpperLimit = MapTB("BG", 113, TextBox119, TextBox105, TextBox91, TextBox77, TextBox63, TextBox49, TextBox35, TextBox21, TextBox3)
        DCC.LowerLimit = MapTB("BL", 113, TextBox118, TextBox104, TextBox90, TextBox76, TextBox62, TextBox48, TextBox34, TextBox20, TextBox2)
        DCC.Remarks = MapTB("BQ", 113, TextBox117, TextBox103, TextBox89, TextBox75, TextBox61, TextBox47, TextBox33, TextBox19, TextBox1)

        LockAutoFields(DCC)

        ' Important: keep DCC last-row inputs editable (shared with DC outputs)
        TextBox12.ReadOnly = False
        TextBox13.ReadOnly = False

        ' --- AC CURRENT (12 rows: 126..137) ---
        ACC.RangeLbl = {L("Label568"), L("Label567"), L("Label566"), L("Label565"), L("Label564"), L("Label563"), L("Label562"), L("Label534"), L("Label532"), L("Label543"), L("Label542"), L("Label539")}
        ACC.Unit1Lbl = {L("Label551"), L("Label550"), L("Label549"), L("Label548"), L("Label547"), L("Label546"), L("Label545"), L("Label533"), L("Label531"), L("Label541"), L("Label540"), L("Label538")}
        ACC.NominalLbl = {L("Label522"), L("Label521"), L("Label520"), L("Label519"), L("Label518"), L("Label517"), L("Label516"), L("Label482"), L("Label480"), L("Label537"), L("Label529"), L("Label441")}
        ACC.Unit2Lbl = {L("Label506"), L("Label505"), L("Label504"), L("Label503"), L("Label502"), L("Label501"), L("Label500"), L("Label481"), L("Label479"), L("Label440"), L("Label439"), L("Label438")}
        ' New:
        ACC.FrequencyLbl = {L("Label478"), L("Label477"), L("Label476"), L("Label475"), L("Label474"), L("Label473"), L("Label472"), L("Label471"), L("Label470"), L("Label436"), L("Label435"), L("Label437")}
        ACC.UnitLbl = {L("Label456"), L("Label455"), L("Label454"), L("Label453"), L("Label452"), L("Label451"), L("Label450"), L("Label449"), L("Label448"), L("Label424"), L("Label423"), L("Label422")}

        ACC.MV1 = MapTB("X", 126, TextBox308, TextBox294, TextBox280, TextBox266, TextBox252, TextBox238, TextBox224, TextBox210, TextBox196, TextBox168, TextBox154, TextBox140)
        ACC.MV2 = MapTB("AC", 126, TextBox307, TextBox293, TextBox279, TextBox265, TextBox251, TextBox237, TextBox223, TextBox209, TextBox195, TextBox167, TextBox153, TextBox139)
        ACC.MV3 = MapTB("AH", 126, TextBox306, TextBox292, TextBox278, TextBox264, TextBox250, TextBox236, TextBox222, TextBox208, TextBox194, TextBox166, TextBox152, TextBox138)

        ACC.Average = MapLBL("AM", 126, Label433, Label432, Label431, Label430, Label429, Label428, Label427, Label426, Label425, Label421, Label420, Label419)
        ACC.Error = MapLBL("AR", 126, Label416, Label415, Label414, Label413, Label412, Label411, Label410, Label409, Label408, Label418, Label417, Label407)
        ACC.FinalUncDecl = MapLBL("DD", 126, Label399, Label398, Label397, Label396, Label395, Label394, Label393, Label392, Label391, Label406, Label405, Label404)

        ACC.Tolerance = MapTB("BB", 126, TextBox305, TextBox291, TextBox277, TextBox263, TextBox249, TextBox235, TextBox221, TextBox207, TextBox193, TextBox165, TextBox151, TextBox137)
        ACC.UpperLimit = MapTB("BG", 126, TextBox301, TextBox287, TextBox273, TextBox259, TextBox245, TextBox231, TextBox217, TextBox203, TextBox189, TextBox161, TextBox147, TextBox133)
        ACC.LowerLimit = MapTB("BL", 126, TextBox300, TextBox286, TextBox272, TextBox258, TextBox244, TextBox230, TextBox216, TextBox202, TextBox188, TextBox160, TextBox146, TextBox132)
        ACC.Remarks = MapTB("BQ", 126, TextBox299, TextBox285, TextBox271, TextBox257, TextBox243, TextBox229, TextBox215, TextBox201, TextBox187, TextBox159, TextBox145, TextBox131)

        LockAutoFields(ACC)

    End Sub

End Class