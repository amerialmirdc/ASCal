Option Strict Off

Imports System.Reflection
Imports System.Runtime.CompilerServices

' =============================================================================
' Module: CalibratingResultMappings
' - Standalone mapping module (no designer/resources)
' - Robust reflection (no CallByName); safe loops; no Tb/tb name clashes
' - Extension method: Me.InitMappings()
' =============================================================================
Module CalibratingResultMappings

    <Extension()>
    Public Sub InitMappings(frm As calibratingResult)

        ' ---------- helpers ---------------------------------------------------
        ' get a private field on the form (e.g., DCV/ACV/RES/DCC/ACC); create if Nothing
        Dim getPg As Func(Of String, Object) =
            Function(fieldName As String)
                Dim t = frm.GetType()
                Dim fi = t.GetField(fieldName, BindingFlags.Instance Or BindingFlags.Public Or BindingFlags.NonPublic Or BindingFlags.IgnoreCase)
                If fi Is Nothing Then Throw New MissingMemberException($"InitMappings: could not find field '{fieldName}' on {t.FullName}.")
                Dim val = fi.GetValue(frm)
                If val Is Nothing Then
                    val = Activator.CreateInstance(fi.FieldType, True)
                    fi.SetValue(frm, val)
                End If
                Return val
            End Function

        ' find controls by name on the form
        Dim FindLabel As Func(Of String, Label) =
            Function(name As String)
                Dim arr = frm.Controls.Find(name, True)
                Return TryCast(If(arr IsNot Nothing AndAlso arr.Length > 0, arr(0), Nothing), Label)
            End Function

        Dim FindTextBox As Func(Of String, TextBox) =
            Function(name As String)
                Dim arr = frm.Controls.Find(name, True)
                Return TryCast(If(arr IsNot Nothing AndAlso arr.Length > 0, arr(0), Nothing), TextBox)
            End Function

        ' reflection setter for private fields of the ParamGroup
        Dim SetPgField As Action(Of Object, String, Object) =
            Sub(pg As Object, fieldName As String, value As Object)
                Dim fi = pg.GetType().GetField(fieldName, BindingFlags.Instance Or BindingFlags.Public Or BindingFlags.NonPublic Or BindingFlags.IgnoreCase)
                If fi Is Nothing Then Throw New MissingMemberException($"ParamGroup is missing field '{fieldName}'.")
                fi.SetValue(pg, value)
            End Sub

        ' map label arrays
        Dim setLabels As Action(Of Object, String, String()) =
            Sub(pg As Object, fieldName As String, names() As String)
                If pg Is Nothing Then Throw New NullReferenceException($"InitMappings: target group is Nothing while setting '{fieldName}'.")
                If names Is Nothing OrElse names.Length = 0 Then
                    SetPgField(pg, fieldName, New Label() {})
                    Exit Sub
                End If
                Dim arr(names.Length - 1) As Label
                For i = 0 To names.Length - 1
                    arr(i) = FindLabel(names(i))
                Next
                SetPgField(pg, fieldName, arr)
            End Sub

        ' map (TextBox, cell) arrays
        Dim setTuples As Action(Of Object, String, String, Integer, String()) =
            Sub(pg As Object, fieldName As String, col As String, startRow As Integer, names() As String)
                If pg Is Nothing Then Throw New NullReferenceException($"InitMappings: target group is Nothing while setting '{fieldName}'.")
                If names Is Nothing OrElse names.Length = 0 Then
                    SetPgField(pg, fieldName, New(TextBox, String)() {})
                    Exit Sub
                End If
                Dim arr(names.Length - 1) As (TextBox, String)
                For i = 0 To names.Length - 1
                    Dim txtBox As TextBox = FindTextBox(names(i))
                    arr(i) = (txtBox, col & (startRow + i).ToString())
                Next
                SetPgField(pg, fieldName, arr)
            End Sub

        Dim lockAuto As Action(Of Object) =
            Sub(pg As Object)
                Dim mi = frm.GetType().GetMethod("LockAutoFields", BindingFlags.Instance Or BindingFlags.NonPublic)
                If mi IsNot Nothing Then mi.Invoke(frm, New Object() {pg})
            End Sub
        ' ----------------------------------------------------------------------

        ' ==============================
        ' DC VOLTAGE  (rows 55 .. 71)
        ' ==============================
        Dim DCV = getPg("DCV")

        setLabels(DCV, "RangeLbl",
            {"Label49", "Label50", "Label51", "Label54", "Label53", "Label52", "Label60",
             "Label59", "Label58", "Label57", "Label56", "Label55", "Label66", "Label65",
             "Label64", "Label63", "Label62"})

        setLabels(DCV, "Unit1Lbl",
            {"Label82", "Label81", "Label80", "Label79", "Label78", "Label77", "Label76",
             "Label75", "Label74", "Label73", "Label72", "Label71", "Label70", "Label69",
             "Label68", "Label67", "Label61"})

        setLabels(DCV, "NominalLbl",
            {"Label99", "Label98", "Label97", "Label96", "Label95", "Label94", "Label93",
             "Label92", "Label91", "Label90", "Label89", "Label88", "Label87", "Label86",
             "Label85", "Label84", "Label83"})

        setLabels(DCV, "Unit2Lbl",
            {"Label133", "Label132", "Label131", "Label130", "Label129", "Label128", "Label127",
             "Label126", "Label125", "Label124", "Label123", "Label122", "Label121", "Label120",
             "Label119", "Label118", "Label117"})

        SetPgField(DCV, "FrequencyLbl", New Label() {})
        SetPgField(DCV, "UnitLbl", New Label() {})

        setTuples(DCV, "MV1", "X", 55,
            {"TextBox5", "TextBox24", "TextBox38", "TextBox52", "TextBox66", "TextBox136", "TextBox122",
             "TextBox108", "TextBox94", "TextBox80", "TextBox234", "TextBox220", "TextBox206", "TextBox192",
             "TextBox178", "TextBox164", "TextBox150"})

        setTuples(DCV, "MV2", "AC", 55,
            {"TextBox6", "TextBox23", "TextBox37", "TextBox51", "TextBox65", "TextBox135", "TextBox121",
             "TextBox107", "TextBox93", "TextBox79", "TextBox233", "TextBox219", "TextBox205", "TextBox191",
             "TextBox177", "TextBox163", "TextBox149"})

        setTuples(DCV, "MV3", "AH", 55,
            {"TextBox7", "TextBox22", "TextBox36", "TextBox50", "TextBox64", "TextBox134", "TextBox120",
             "TextBox106", "TextBox92", "TextBox78", "TextBox232", "TextBox218", "TextBox204", "TextBox190",
             "TextBox176", "TextBox162", "TextBox148"})

        setTuples(DCV, "Tolerance", "BB", 55,
            {"TextBox11", "TextBox18", "TextBox32", "TextBox46", "TextBox60", "TextBox130", "TextBox116",
             "TextBox102", "TextBox88", "TextBox74", "TextBox228", "TextBox214", "TextBox200", "TextBox186",
             "TextBox172", "TextBox158", "TextBox144"})

        setTuples(DCV, "UpperLimit", "BG", 55,
            {"TextBox14", "TextBox17", "TextBox31", "TextBox45", "TextBox59", "TextBox129", "TextBox115",
             "TextBox101", "TextBox87", "TextBox73", "TextBox227", "TextBox213", "TextBox199", "TextBox185",
             "TextBox171", "TextBox157", "TextBox143"})

        setTuples(DCV, "LowerLimit", "BL", 55,
            {"TextBox13", "TextBox16", "TextBox30", "TextBox44", "TextBox58", "TextBox128", "TextBox114",
             "TextBox100", "TextBox86", "TextBox72", "TextBox226", "TextBox212", "TextBox198", "TextBox184",
             "TextBox170", "TextBox156", "TextBox142"})

        setTuples(DCV, "Remarks", "BQ", 55,
            {"TextBox12", "TextBox15", "TextBox29", "TextBox43", "TextBox57", "TextBox127", "TextBox113",
             "TextBox99", "TextBox85", "TextBox71", "TextBox225", "TextBox211", "TextBox197", "TextBox183",
             "TextBox169", "TextBox155", "TextBox141"})

        setTuples_Labels(DCV, "Average", "AM", 55,
            {"Label100", "Label101", "Label103", "Label102", "Label107", "Label106", "Label105",
             "Label104", "Label115", "Label114", "Label113", "Label112", "Label111", "Label110",
             "Label109", "Label108", "Label116"})

        setTuples_Labels(DCV, "Error", "AR", 55,
            {"Label134", "Label135", "Label137", "Label136", "Label141", "Label140", "Label139", "Label138",
             "Label149", "Label148", "Label147", "Label146", "Label145", "Label144", "Label143", "Label142", "Label150"})

        setTuples_Labels(DCV, "FinalUncDecl", "DD", 55,
            {"Label151", "Label152", "Label154", "Label153", "Label158", "Label157", "Label156", "Label155",
             "Label166", "Label165", "Label164", "Label163", "Label162", "Label161", "Label160", "Label159", "Label167"})

        lockAuto(DCV)

        ' ==============================
        ' AC VOLTAGE  (rows 76 .. 97)
        ' ==============================
        Dim ACV = getPg("ACV")

        setLabels(ACV, "RangeLbl",
            {"Label201", "Label200", "Label199", "Label203", "Label198", "Label197", "Label196", "Label205",
             "Label195", "Label194", "Label193", "Label192", "Label191", "Label207", "Label190", "Label189",
             "Label188", "Label209", "Label187", "Label186", "Label185", "Label211"})

        setLabels(ACV, "Unit1Lbl",
            {"Label184", "Label183", "Label182", "Label202", "Label181", "Label180", "Label179", "Label204",
             "Label178", "Label177", "Label176", "Label175", "Label174", "Label206", "Label173", "Label172",
             "Label171", "Label208", "Label170", "Label169", "Label168", "Label210"})

        setLabels(ACV, "NominalLbl",
            {"Label245", "Label256", "Label244", "Label243", "Label254", "Label242", "Label241", "Label240",
             "Label239", "Label252", "Label236", "Label238", "Label237", "Label235", "Label234", "Label250",
             "Label233", "Label232", "Label231", "Label248", "Label230", "Label229"})

        setLabels(ACV, "Unit2Lbl",
            {"Label228", "Label255", "Label227", "Label226", "Label253", "Label225", "Label224", "Label223",
             "Label222", "Label251", "Label219", "Label221", "Label220", "Label218", "Label217", "Label249",
             "Label216", "Label215", "Label214", "Label29", "Label213", "Label212"})

        setLabels(ACV, "FrequencyLbl",
            {"Label258", "Label257", "Label260", "Label259", "Label264", "Label263", "Label262", "Label261",
             "Label272", "Label271", "Label270", "Label269", "Label268", "Label267", "Label266", "Label265",
             "Label278", "Label277", "Label276", "Label275", "Label274", "Label273"})

        setLabels(ACV, "UnitLbl",
            {"Label280", "Label279", "Label282", "Label281", "Label286", "Label285", "Label284", "Label283",
             "Label294", "Label293", "Label292", "Label291", "Label290", "Label289", "Label288", "Label287",
             "Label302", "Label301", "Label300", "Label299", "Label298", "Label297"})

        setTuples(ACV, "MV1", "X", 76,
            {"TextBox472", "TextBox458", "TextBox444", "TextBox430", "TextBox416", "TextBox402", "TextBox388",
             "TextBox374", "TextBox360", "TextBox346", "TextBox332", "TextBox318", "TextBox304", "TextBox290",
             "TextBox276", "TextBox262", "TextBox248", "TextBox542", "TextBox528", "TextBox514", "TextBox500", "TextBox486"})

        setTuples(ACV, "MV2", "AC", 76,
            {"TextBox471", "TextBox457", "TextBox443", "TextBox429", "TextBox415", "TextBox401", "TextBox387",
             "TextBox373", "TextBox359", "TextBox345", "TextBox331", "TextBox317", "TextBox303", "TextBox289",
             "TextBox275", "TextBox261", "TextBox247", "TextBox541", "TextBox527", "TextBox513", "TextBox499", "TextBox485"})

        setTuples(ACV, "MV3", "AH", 76,
            {"TextBox470", "TextBox456", "TextBox442", "TextBox428", "TextBox414", "TextBox400", "TextBox386",
             "TextBox372", "TextBox358", "TextBox344", "TextBox330", "TextBox316", "TextBox302", "TextBox288",
             "TextBox274", "TextBox260", "TextBox246", "TextBox540", "TextBox526", "TextBox512", "TextBox498", "TextBox484"})

        setTuples(ACV, "Tolerance", "BB", 76,
            {"TextBox466", "TextBox452", "TextBox438", "TextBox424", "TextBox410", "TextBox396", "TextBox382", "TextBox368",
             "TextBox354", "TextBox340", "TextBox326", "TextBox312", "TextBox298", "TextBox284", "TextBox270", "TextBox256",
             "TextBox242", "TextBox536", "TextBox522", "TextBox508", "TextBox494", "TextBox480"})

        setTuples(ACV, "UpperLimit", "BG", 76,
            {"TextBox465", "TextBox451", "TextBox437", "TextBox423", "TextBox409", "TextBox395", "TextBox381", "TextBox367",
             "TextBox353", "TextBox339", "TextBox325", "TextBox311", "TextBox297", "TextBox283", "TextBox269", "TextBox255",
             "TextBox241", "TextBox535", "TextBox521", "TextBox507", "TextBox493", "TextBox479"})

        setTuples(ACV, "LowerLimit", "BL", 76,
            {"TextBox464", "TextBox450", "TextBox436", "TextBox422", "TextBox408", "TextBox394", "TextBox380", "TextBox366",
             "TextBox352", "TextBox338", "TextBox324", "TextBox310", "TextBox296", "TextBox282", "TextBox268", "TextBox254",
             "TextBox240", "TextBox534", "TextBox520", "TextBox506", "TextBox492", "TextBox478"})

        setTuples(ACV, "Remarks", "BQ", 76,
            {"TextBox463", "TextBox449", "TextBox435", "TextBox421", "TextBox407", "TextBox393", "TextBox379", "TextBox365",
             "TextBox351", "TextBox337", "TextBox323", "TextBox309", "TextBox295", "TextBox281", "TextBox267", "TextBox253",
             "TextBox239", "TextBox533", "TextBox519", "TextBox505", "TextBox491", "TextBox477"})

        setTuples_Labels(ACV, "Average", "AM", 76,
            {"Label352", "Label351", "Label350", "Label349", "Label348", "Label347", "Label346", "Label345", "Label344",
             "Label343", "Label342", "Label341", "Label340", "Label339", "Label338", "Label337", "Label336", "Label367",
             "Label366", "Label365", "Label364", "Label363"})

        setTuples_Labels(ACV, "Error", "AR", 76,
            {"Label335", "Label334", "Label333", "Label332", "Label331", "Label330", "Label329", "Label328", "Label327",
             "Label326", "Label325", "Label324", "Label323", "Label322", "Label321", "Label320", "Label319", "Label362",
             "Label361", "Label360", "Label359", "Label358"})

        setTuples_Labels(ACV, "FinalUncDecl", "DD", 76,
            {"Label318", "Label317", "Label316", "Label315", "Label314", "Label313", "Label312", "Label311", "Label310",
             "Label309", "Label308", "Label307", "Label306", "Label305", "Label304", "Label303", "Label296", "Label357",
             "Label356", "Label355", "Label354", "Label353"})

        lockAuto(ACV)

        ' ==============================
        ' RESISTANCE  (rows 102 .. 108)
        ' ==============================
        Dim RES = getPg("RES")

        setLabels(RES, "RangeLbl", {"Label584", "Label587", "Label589", "Label595", "Label593", "Label591", "Label597"})
        setLabels(RES, "Unit1Lbl", {"Label585", "Label586", "Label588", "Label594", "Label592", "Label590", "Label596"})
        setLabels(RES, "NominalLbl", {"Label611", "Label609", "Label607", "Label605", "Label603", "Label601", "Label599"})
        setLabels(RES, "Unit2Lbl", {"Label610", "Label608", "Label606", "Label604", "Label602", "Label600", "Label598"})
        SetPgField(RES, "FrequencyLbl", New Label() {})
        SetPgField(RES, "UnitLbl", New Label() {})

        setTuples(RES, "MV1", "X", 102, {"TextBox780", "TextBox766", "TextBox752", "TextBox738", "TextBox724", "TextBox710", "TextBox696"})
        setTuples(RES, "MV2", "AC", 102, {"TextBox779", "TextBox765", "TextBox751", "TextBox737", "TextBox723", "TextBox709", "TextBox695"})
        setTuples(RES, "MV3", "AH", 102, {"TextBox778", "TextBox764", "TextBox750", "TextBox736", "TextBox722", "TextBox708", "TextBox694"})

        setTuples(RES, "Tolerance", "BB", 102, {"TextBox774", "TextBox760", "TextBox746", "TextBox732", "TextBox718", "TextBox704", "TextBox690"})
        setTuples(RES, "UpperLimit", "BG", 102, {"TextBox773", "TextBox759", "TextBox745", "TextBox731", "TextBox717", "TextBox703", "TextBox689"})
        setTuples(RES, "LowerLimit", "BL", 102, {"TextBox772", "TextBox758", "TextBox744", "TextBox730", "TextBox716", "TextBox702", "TextBox688"})
        setTuples(RES, "Remarks", "BQ", 102, {"TextBox771", "TextBox757", "TextBox743", "TextBox729", "TextBox715", "TextBox701", "TextBox687"})

        setTuples_Labels(RES, "Average", "AM", 102, {"Label553", "Label556", "Label616", "Label559", "Label628", "Label625", "Label622"})
        setTuples_Labels(RES, "Error", "AR", 102, {"Label552", "Label555", "Label561", "Label558", "Label627", "Label624", "Label621"})
        setTuples_Labels(RES, "FinalUncDecl", "DD", 102, {"Label544", "Label554", "Label560", "Label557", "Label626", "Label623", "Label620"})

        lockAuto(RES)

        ' ==============================
        ' DC CURRENT  (rows 113 .. 121)
        ' ==============================
        Dim DCC = getPg("DCC")

        setLabels(DCC, "RangeLbl", {"Label508", "Label507", "Label499", "Label487", "Label498", "Label497", "Label496", "Label485", "Label495"})
        setLabels(DCC, "Unit1Lbl", {"Label494", "Label493", "Label492", "Label486", "Label491", "Label490", "Label489", "Label484", "Label488"})
        setLabels(DCC, "NominalLbl", {"Label468", "Label443", "Label467", "Label466", "Label445", "Label465", "Label464", "Label463", "Label462"})
        setLabels(DCC, "Unit2Lbl", {"Label461", "Label460", "Label459", "Label444", "Label458", "Label457", "Label446", "Label442", "Label446"})
        SetPgField(DCC, "FrequencyLbl", New Label() {})
        SetPgField(DCC, "UnitLbl", New Label() {})

        setTuples(DCC, "MV1", "X", 113, {"TextBox126", "TextBox112", "TextBox98", "TextBox84", "TextBox70", "TextBox56", "TextBox42", "TextBox28", "TextBox10"})
        setTuples(DCC, "MV2", "AC", 113, {"TextBox125", "TextBox111", "TextBox97", "TextBox83", "TextBox69", "TextBox55", "TextBox41", "TextBox27", "TextBox9"})
        setTuples(DCC, "MV3", "AH", 113, {"TextBox124", "TextBox110", "TextBox96", "TextBox82", "TextBox68", "TextBox54", "TextBox40", "TextBox26", "TextBox8"})

        setTuples(DCC, "Tolerance", "BB", 113, {"TextBox123", "TextBox109", "TextBox95", "TextBox81", "TextBox67", "TextBox53", "TextBox39", "TextBox25", "TextBox4"})
        setTuples(DCC, "UpperLimit", "BG", 113, {"TextBox119", "TextBox105", "TextBox91", "TextBox77", "TextBox63", "TextBox49", "TextBox35", "TextBox21", "TextBox3"})
        setTuples(DCC, "LowerLimit", "BL", 113, {"TextBox118", "TextBox104", "TextBox90", "TextBox76", "TextBox62", "TextBox48", "TextBox34", "TextBox20", "TextBox2"})
        setTuples(DCC, "Remarks", "BQ", 113, {"TextBox117", "TextBox103", "TextBox89", "TextBox75", "TextBox61", "TextBox47", "TextBox33", "TextBox19", "TextBox1"})

        setTuples_Labels(DCC, "Average", "AM", 113, {"Label403", "Label402", "Label401", "Label400", "Label390", "Label389", "Label388", "Label387", "Label386"})
        setTuples_Labels(DCC, "Error", "AR", 113, {"Label385", "Label384", "Label383", "Label382", "Label381", "Label380", "Label379", "Label378", "Label377"})
        setTuples_Labels(DCC, "FinalUncDecl", "DD", 113, {"Label376", "Label375", "Label374", "Label373", "Label372", "Label371", "Label370", "Label369", "Label368"})

        lockAuto(DCC)

        ' keep these editable if needed (from earlier behavior)
        Dim tb12 = FindTextBox("TextBox12") : If tb12 IsNot Nothing Then tb12.ReadOnly = False
        Dim tb13 = FindTextBox("TextBox13") : If tb13 IsNot Nothing Then tb13.ReadOnly = False

        ' ==============================
        ' AC CURRENT  (rows 126 .. 137)
        ' ==============================
        Dim ACC = getPg("ACC")

        setLabels(ACC, "RangeLbl", {"Label568", "Label567", "Label566", "Label534", "Label565", "Label564", "Label563", "Label532", "Label562", "Label543", "Label539", "Label542"})
        setLabels(ACC, "Unit1Lbl", {"Label551", "Label550", "Label549", "Label533", "Label548", "Label547", "Label546", "Label531", "Label545", "Label541", "Label538", "Label540"})
        setLabels(ACC, "NominalLbl", {"Label522", "Label480", "Label521", "Label520", "Label482", "Label519", "Label518", "Label517", "Label516", "Label537", "Label529", "Label441"})
        setLabels(ACC, "Unit2Lbl", {"Label506", "Label479", "Label505", "Label504", "Label481", "Label503", "Label502", "Label501", "Label500", "Label440", "Label439", "Label438"})

        setLabels(ACC, "FrequencyLbl", {"Label478", "Label477", "Label476", "Label475", "Label474", "Label473", "Label472", "Label471", "Label470", "Label436", "Label435", "Label437"})
        setLabels(ACC, "UnitLbl", {"Label456", "Label455", "Label454", "Label453", "Label452", "Label451", "Label450", "Label449", "Label448", "Label424", "Label423", "Label422"})

        setTuples(ACC, "MV1", "X", 126, {"TextBox308", "TextBox294", "TextBox280", "TextBox266", "TextBox252", "TextBox238", "TextBox224", "TextBox210", "TextBox196", "TextBox168", "TextBox154", "TextBox140"})
        setTuples(ACC, "MV2", "AC", 126, {"TextBox307", "TextBox293", "TextBox279", "TextBox265", "TextBox251", "TextBox237", "TextBox223", "TextBox209", "TextBox195", "TextBox167", "TextBox153", "TextBox139"})
        setTuples(ACC, "MV3", "AH", 126, {"TextBox306", "TextBox292", "TextBox278", "TextBox264", "TextBox250", "TextBox236", "TextBox222", "TextBox208", "TextBox194", "TextBox166", "TextBox152", "TextBox138"})

        setTuples(ACC, "Tolerance", "BB", 126, {"TextBox305", "TextBox291", "TextBox277", "TextBox263", "TextBox249", "TextBox235", "TextBox221", "TextBox207", "TextBox193", "TextBox165", "TextBox151", "TextBox137"})
        setTuples(ACC, "UpperLimit", "BG", 126, {"TextBox301", "TextBox287", "TextBox273", "TextBox259", "TextBox245", "TextBox231", "TextBox217", "TextBox203", "TextBox189", "TextBox161", "TextBox147", "TextBox133"})
        setTuples(ACC, "LowerLimit", "BL", 126, {"TextBox300", "TextBox286", "TextBox272", "TextBox258", "TextBox244", "TextBox230", "TextBox216", "TextBox202", "TextBox188", "TextBox160", "TextBox146", "TextBox132"})
        setTuples(ACC, "Remarks", "BQ", 126, {"TextBox299", "TextBox285", "TextBox271", "TextBox257", "TextBox243", "TextBox229", "TextBox215", "TextBox201", "TextBox187", "TextBox159", "TextBox145", "TextBox131"})

        setTuples_Labels(ACC, "Average", "AM", 126, {"Label433", "Label432", "Label431", "Label430", "Label429", "Label428", "Label427", "Label426", "Label425", "Label421", "Label420", "Label419"})
        setTuples_Labels(ACC, "Error", "AR", 126, {"Label416", "Label415", "Label414", "Label413", "Label412", "Label411", "Label410", "Label409", "Label408", "Label418", "Label417", "Label407"})
        setTuples_Labels(ACC, "FinalUncDecl", "DD", 126, {"Label399", "Label398", "Label397", "Label396", "Label395", "Label394", "Label393", "Label392", "Label391", "Label406", "Label405", "Label404"})

        lockAuto(ACC)

    End Sub

    ' -- helper to map label+cell arrays (Average/Error/FinalUnc)
    Private Sub setTuples_Labels(pg As Object, fieldName As String, col As String, startRow As Integer, names() As String)
        If names Is Nothing OrElse names.Length = 0 Then
            Dim fi0 = pg.GetType().GetField(fieldName, BindingFlags.Instance Or BindingFlags.Public Or BindingFlags.NonPublic Or BindingFlags.IgnoreCase)
            If fi0 Is Nothing Then Throw New MissingMemberException($"ParamGroup is missing field '{fieldName}'.")
            fi0.SetValue(pg, New(Label, String)() {})
            Exit Sub
        End If

        Dim arr(names.Length - 1) As (Label, String)
        For i = 0 To names.Length - 1
            Dim ctl = GetFirstControlFrom(pg, names(i))
            arr(i) = (TryCast(ctl, Label), col & (startRow + i).ToString())
        Next

        Dim fi = pg.GetType().GetField(fieldName, BindingFlags.Instance Or BindingFlags.Public Or BindingFlags.NonPublic Or BindingFlags.IgnoreCase)
        If fi Is Nothing Then Throw New MissingMemberException($"ParamGroup is missing field '{fieldName}'.")
        fi.SetValue(pg, arr)
    End Sub

    ' Find by name using Application.OpenForms (works even though pg is a private nested type)
    Private Function GetFirstControlFrom(pg As Object, name As String) As Control
        For Each f As Form In Application.OpenForms
            Dim arr = f.Controls.Find(name, True)
            If arr IsNot Nothing AndAlso arr.Length > 0 Then Return TryCast(arr(0), Control)
        Next
        Return Nothing
    End Function

End Module