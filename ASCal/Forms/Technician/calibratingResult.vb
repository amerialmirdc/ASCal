' calibratingResult.vb
Option Strict Off

Public Class calibratingResult

    ' --- real-time compute debounce ---
    Private dcComputeTimer As Timer

    ' --- Excel context (from CalRowModule) ---
    Private ctxDc As CalRowModule.RowContext

    ' --- DC VOLTAGE: output labels (AVERAGE/ERROR/FINAL UNCERTAINTY) ---
    Private DC_AVG As (lbl As Label, cell As String)()

    Private DC_ERR As (lbl As Label, cell As String)()
    Private DC_FU As (lbl As Label, cell As String)()

    ' --- DC VOLTAGE: auto-filled outputs (Tolerance / Upper / Lower / Remarks) ---
    Private DC_TOL As (tb As TextBox, cell As String)()

    Private DC_UPPER As (tb As TextBox, cell As String)()
    Private DC_LOWER As (tb As TextBox, cell As String)()
    Private DC_REMARKS As (tb As TextBox, cell As String)()

    ' --- state for targeted compute ---
    Private currentDcRowIdx As Integer = -1   ' 0..16 (index in arrays)

    Private currentExcelRow As Integer = -1   ' 55..71

    ' --- Parameter buckets for a voltage section ---
    Private Class ParamGroup

        ' Inputs (user-entered)
        Public Frequency As (tb As TextBox, cell As String)()  ' optional per-row freq (if you have it)

        Public MV1 As (tb As TextBox, cell As String)()
        Public MV2 As (tb As TextBox, cell As String)()
        Public MV3 As (tb As TextBox, cell As String)()

        ' Computed labels (outputs)
        Public Average As (lbl As Label, cell As String)()

        Public [Error] As (lbl As Label, cell As String)()
        Public FinalUncDecl As (lbl As Label, cell As String)()

        ' Auto-filled textboxes (outputs mirrored from Excel)
        Public Tolerance As (tb As TextBox, cell As String)()

        Public UpperLimit As (tb As TextBox, cell As String)()
        Public LowerLimit As (tb As TextBox, cell As String)()
        Public Remarks As (tb As TextBox, cell As String)()
    End Class

    ' Parameters
    Private DC As New ParamGroup()

    Private AC As New ParamGroup()
    Private RES As New ParamGroup()
    Private DCC As New ParamGroup()  ' DC CURRENT
    Private ACC As New ParamGroup()  ' AC CURRENT

    ' which section fired (DC or AC) and which row within that section
    Private currentGroup As ParamGroup = Nothing

    Private currentRowIdx As Integer = -1

    Private Function MapTB(col As String, startRow As Integer, ParamArray boxes() As TextBox) _
    As (tb As TextBox, cell As String)()
        Dim a(boxes.Length - 1) As (TextBox, String)
        For i = 0 To boxes.Length - 1
            a(i) = (boxes(i), col & (startRow + i).ToString())
        Next
        Return a
    End Function

    Private Function MapLBL(col As String, startRow As Integer, ParamArray labels() As Label) _
    As (lbl As Label, cell As String)()
        Dim a(labels.Length - 1) As (Label, String)
        For i = 0 To labels.Length - 1
            a(i) = (labels(i), col & (startRow + i).ToString())
        Next
        Return a
    End Function

    Private Sub LockAutoFields(g As ParamGroup)
        If g Is Nothing Then Exit Sub
        Dim lockOne = Sub(tb As TextBox)
                          tb.ReadOnly = True : tb.TabStop = False : tb.ShortcutsEnabled = False
                          tb.BackColor = SystemColors.ControlLight : tb.Cursor = Cursors.Default
                      End Sub
        If g.Tolerance IsNot Nothing Then For Each p In g.Tolerance : lockOne(p.tb) : Next
        If g.UpperLimit IsNot Nothing Then For Each p In g.UpperLimit : lockOne(p.tb) : Next
        If g.LowerLimit IsNot Nothing Then For Each p In g.LowerLimit : lockOne(p.tb) : Next
        If g.Remarks IsNot Nothing Then For Each p In g.Remarks : lockOne(p.tb) : Next
    End Sub

    Private Sub WriteInputsRow(ws As Object, g As ParamGroup, i As Integer)
        If ws Is Nothing OrElse g Is Nothing OrElse i < 0 Then Exit Sub
        If g.MV1 IsNot Nothing AndAlso i < g.MV1.Length Then WriteCell(ws, g.MV1(i).cell, g.MV1(i).tb.Text)
        If g.MV2 IsNot Nothing AndAlso i < g.MV2.Length Then WriteCell(ws, g.MV2(i).cell, g.MV2(i).tb.Text)
        If g.MV3 IsNot Nothing AndAlso i < g.MV3.Length Then WriteCell(ws, g.MV3(i).cell, g.MV3(i).tb.Text)
        ' If you add per-row Frequency later, mirror it here similarly with length checks.
    End Sub

    Private Sub ReadOutputsRow(ws As Object, g As ParamGroup, i As Integer)
        If ws Is Nothing OrElse g Is Nothing OrElse i < 0 Then Exit Sub
        If g.Average IsNot Nothing AndAlso i < g.Average.Length Then g.Average(i).lbl.Text = ReadCell(ws, g.Average(i).cell)
        If g.Error IsNot Nothing AndAlso i < g.Error.Length Then g.Error(i).lbl.Text = ReadCell(ws, g.Error(i).cell)
        If g.FinalUncDecl IsNot Nothing AndAlso i < g.FinalUncDecl.Length Then g.FinalUncDecl(i).lbl.Text = ReadCell(ws, g.FinalUncDecl(i).cell)

        If g.Tolerance IsNot Nothing AndAlso i < g.Tolerance.Length Then g.Tolerance(i).tb.Text = ReadCell(ws, g.Tolerance(i).cell)
        If g.UpperLimit IsNot Nothing AndAlso i < g.UpperLimit.Length Then g.UpperLimit(i).tb.Text = ReadCell(ws, g.UpperLimit(i).cell)
        If g.LowerLimit IsNot Nothing AndAlso i < g.LowerLimit.Length Then g.LowerLimit(i).tb.Text = ReadCell(ws, g.LowerLimit(i).cell)
        If g.Remarks IsNot Nothing AndAlso i < g.Remarks.Length Then
            Dim tb = g.Remarks(i).tb
            tb.Text = ReadCell(ws, g.Remarks(i).cell)
            ApplyPassFailColor(tb)  ' <— color it based on text
        End If
    End Sub

    Private Sub calibratingResult_Load(sender As Object, e As EventArgs) Handles MyBase.Load

        ' Make sure start position is manual
        Me.StartPosition = FormStartPosition.Manual

        ' Remove designer overrides
        Me.MaximumSize = New Size(0, 0)
        Me.MinimumSize = New Size(0, 0)

        ' Get working area excluding the taskbar
        Dim currentScreen As Screen = Screen.FromControl(Me)
        Dim workingArea As Rectangle = currentScreen.WorkingArea

        ' Apply correct size and location
        Me.Bounds = Screen.FromControl(Me).WorkingArea

        ' DC inputs
        DC.MV1 = {(TextBox5, "X55"), (TextBox24, "X56"), (TextBox38, "X57"), (TextBox52, "X58"), (TextBox66, "X59"),
          (TextBox136, "X60"), (TextBox122, "X61"), (TextBox108, "X62"), (TextBox94, "X63"), (TextBox80, "X64"),
          (TextBox234, "X65"), (TextBox220, "X66"), (TextBox206, "X67"), (TextBox192, "X68"), (TextBox178, "X69"),
          (TextBox164, "X70"), (TextBox150, "X71")}
        DC.MV2 = {(TextBox6, "AC55"), (TextBox23, "AC56"), (TextBox37, "AC57"), (TextBox51, "AC58"), (TextBox65, "AC59"),
          (TextBox135, "AC60"), (TextBox121, "AC61"), (TextBox107, "AC62"), (TextBox93, "AC63"), (TextBox79, "AC64"),
          (TextBox233, "AC65"), (TextBox219, "AC66"), (TextBox205, "AC67"), (TextBox191, "AC68"), (TextBox177, "AC69"),
          (TextBox163, "AC70"), (TextBox149, "AC71")}
        DC.MV3 = {(TextBox7, "AH55"), (TextBox22, "AH56"), (TextBox36, "AH57"), (TextBox50, "AH58"), (TextBox64, "AH59"),
          (TextBox134, "AH60"), (TextBox120, "AH61"), (TextBox106, "AH62"), (TextBox92, "AH63"), (TextBox78, "AH64"),
          (TextBox232, "AH65"), (TextBox218, "AH66"), (TextBox204, "AH67"), (TextBox190, "AH68"), (TextBox176, "AH69"),
          (TextBox162, "AH70"), (TextBox148, "AH71")}
        ' DC computed labels
        DC.Average = {(Label100, "AM55"), (Label101, "AM56"), (Label103, "AM57"), (Label102, "AM58"), (Label107, "AM59"),
              (Label106, "AM60"), (Label105, "AM61"), (Label104, "AM62"), (Label115, "AM63"), (Label114, "AM64"),
              (Label113, "AM65"), (Label112, "AM66"), (Label111, "AM67"), (Label110, "AM68"), (Label109, "AM69"),
              (Label108, "AM70"), (Label116, "AM71")}
        DC.Error = {(Label134, "AR55"), (Label135, "AR56"), (Label137, "AR57"), (Label136, "AR58"), (Label141, "AR59"),
              (Label140, "AR60"), (Label139, "AR61"), (Label138, "AR62"), (Label149, "AR63"), (Label148, "AR64"),
              (Label147, "AR65"), (Label146, "AR66"), (Label145, "AR67"), (Label144, "AR68"), (Label143, "AR69"),
              (Label142, "AR70"), (Label150, "AR71")}
        DC.FinalUncDecl = {(Label151, "DD55"), (Label152, "DD56"), (Label154, "DD57"), (Label153, "DD58"), (Label158, "DD59"),
                   (Label157, "DD60"), (Label156, "DD61"), (Label155, "DD62"), (Label166, "DD63"), (Label165, "DD64"),
                   (Label164, "DD65"), (Label163, "DD66"), (Label162, "DD67"), (Label161, "DD68"), (Label160, "DD69"),
                   (Label159, "DD70"), (Label167, "DD71")}
        ' DC mirrored outputs
        DC.Tolerance = {(TextBox11, "BB55"), (TextBox18, "BB56"), (TextBox32, "BB57"), (TextBox46, "BB58"), (TextBox60, "BB59"),
                 (TextBox130, "BB60"), (TextBox116, "BB61"), (TextBox102, "BB62"), (TextBox88, "BB63"), (TextBox74, "BB64"),
                 (TextBox228, "BB65"), (TextBox214, "BB66"), (TextBox200, "BB67"), (TextBox186, "BB68"), (TextBox172, "BB69"),
                 (TextBox158, "BB70"), (TextBox144, "BB71")}
        DC.UpperLimit = {(TextBox14, "BG55"), (TextBox17, "BG56"), (TextBox31, "BG57"), (TextBox45, "BG58"), (TextBox59, "BG59"),
                 (TextBox129, "BG60"), (TextBox115, "BG61"), (TextBox101, "BG62"), (TextBox87, "BG63"), (TextBox73, "BG64"),
                 (TextBox227, "BG65"), (TextBox213, "BG66"), (TextBox199, "BG67"), (TextBox185, "BG68"), (TextBox171, "BG69"),
                 (TextBox157, "BG70"), (TextBox143, "BG71")}
        DC.LowerLimit = {(TextBox13, "BL55"), (TextBox16, "BL56"), (TextBox30, "BL57"), (TextBox44, "BL58"), (TextBox58, "BL59"),
                 (TextBox128, "BL60"), (TextBox114, "BL61"), (TextBox100, "BL62"), (TextBox86, "BL63"), (TextBox72, "BL64"),
                 (TextBox226, "BL65"), (TextBox212, "BL66"), (TextBox198, "BL67"), (TextBox184, "BL68"), (TextBox170, "BL69"),
                 (TextBox156, "BL70"), (TextBox142, "BL71")}
        DC.Remarks = {(TextBox12, "BQ55"), (TextBox15, "BQ56"), (TextBox29, "BQ57"), (TextBox43, "BQ58"), (TextBox57, "BQ59"),
                 (TextBox127, "BQ60"), (TextBox113, "BQ61"), (TextBox99, "BQ62"), (TextBox85, "BQ63"), (TextBox71, "BQ64"),
                 (TextBox225, "BQ65"), (TextBox211, "BQ66"), (TextBox197, "BQ67"), (TextBox183, "BQ68"), (TextBox169, "BQ69"),
                 (TextBox155, "BQ70"), (TextBox141, "BQ71")}

        LockAutoFields(DC)

        ' AC inputs
        AC.MV1 = MapTB("X", 76, TextBox472, TextBox458, TextBox444, TextBox430, TextBox416, TextBox402, TextBox388, TextBox374,
                        TextBox360, TextBox346, TextBox332, TextBox318, TextBox304, TextBox290, TextBox276, TextBox262,
                        TextBox248, TextBox542, TextBox528, TextBox514, TextBox500, TextBox486)
        AC.MV2 = MapTB("AC", 76, TextBox471, TextBox457, TextBox443, TextBox429, TextBox415, TextBox401, TextBox387, TextBox373,
                         TextBox359, TextBox345, TextBox331, TextBox317, TextBox303, TextBox289, TextBox275, TextBox261,
                         TextBox247, TextBox541, TextBox527, TextBox513, TextBox499, TextBox485)
        AC.MV3 = MapTB("AH", 76, TextBox470, TextBox456, TextBox442, TextBox428, TextBox414, TextBox400, TextBox386, TextBox372,
                         TextBox358, TextBox344, TextBox330, TextBox316, TextBox302, TextBox288, TextBox274, TextBox260,
                         TextBox246, TextBox540, TextBox526, TextBox512, TextBox498, TextBox484)
        ' AC computed labels
        AC.Average = MapLBL("AM", 76, Label352, Label351, Label350, Label349, Label348, Label347, Label346, Label345, Label344,
                                  Label343, Label342, Label341, Label340, Label339, Label338, Label337, Label336, Label367,
                                  Label366, Label365, Label364, Label363)
        AC.Error = MapLBL("AR", 76, Label335, Label334, Label333, Label332, Label331, Label330, Label329, Label328, Label327,
                                  Label326, Label325, Label324, Label323, Label322, Label321, Label320, Label319, Label362,
                                  Label361, Label360, Label359, Label358)
        AC.FinalUncDecl = MapLBL("DD", 76, Label318, Label317, Label316, Label315, Label314, Label313, Label312, Label311, Label310,
                                  Label309, Label308, Label307, Label306, Label305, Label304, Label303, Label296, Label357,
                                  Label356, Label355, Label354, Label353)
        ' AC mirrored outputs
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
        DCC.MV2 = MapTB("AC", 113, TextBox125, TextBox111, TextBox97, TextBox83, TextBox69, TextBox55, TextBox41, TextBox27, TextBox13)
        DCC.MV3 = MapTB("AH", 113, TextBox124, TextBox110, TextBox96, TextBox82, TextBox68, TextBox54, TextBox40, TextBox26, TextBox12)

        ' Computed labels
        DCC.Average = MapLBL("AM", 113, Label403, Label402, Label401, Label400, Label390, Label389, Label388, Label387, Label386)
        DCC.Error = MapLBL("AR", 113, Label385, Label384, Label383, Label382, Label381, Label380, Label379, Label378, Label377)
        DCC.FinalUncDecl = MapLBL("DD", 113, Label376, Label375, Label374, Label373, Label372, Label371, Label370, Label369, Label368)

        ' Mirrored outputs
        DCC.Tolerance = MapTB("BB", 113, TextBox123, TextBox109, TextBox95, TextBox81, TextBox67, TextBox53, TextBox39, TextBox25, TextBox4)
        DCC.UpperLimit = MapTB("BG", 113, TextBox119, TextBox105, TextBox91, TextBox77, TextBox63, TextBox49, TextBox35, TextBox21, TextBox3)
        DCC.LowerLimit = MapTB("BL", 113, TextBox118, TextBox104, TextBox90, TextBox76, TextBox62, TextBox48, TextBox34, TextBox20, TextBox2)
        DCC.Remarks = MapTB("BQ", 113, TextBox118, TextBox104, TextBox90, TextBox76, TextBox62, TextBox48, TextBox34, TextBox20, TextBox1)

        LockAutoFields(DCC)

        ' ================== AC CURRENT ==================
        ' Inputs
        ACC.MV1 = MapTB("X", 126, TextBox308, TextBox294, TextBox280, TextBox266, TextBox252, TextBox238, TextBox224, TextBox210, TextBox196, TextBox168, TextBox154, TextBox140)
        ACC.MV2 = MapTB("AC", 126, TextBox307, TextBox293, TextBox279, TextBox265, TextBox251, TextBox237, TextBox223, TextBox209, TextBox195, TextBox167, TextBox153, TextBox139)
        ACC.MV3 = MapTB("AH", 126, TextBox306, TextBox292, TextBox278, TextBox264, TextBox250, TextBox236, TextBox222, TextBox208, TextBox194, TextBox166, TextBox152, TextBox138)

        ' Computed labels
        ACC.Average = MapLBL("AM", 126, Label433, Label432, Label431, Label430, Label429, Label428, Label427, Label426, Label425, Label422, Label421, Label420)
        ACC.Error = MapLBL("AR", 126, Label416, Label415, Label414, Label413, Label412, Label411, Label410, Label409, Label408, Label418, Label417, Label407)
        ACC.FinalUncDecl = MapLBL("DD", 126, Label399, Label398, Label397, Label396, Label395, Label394, Label393, Label392, Label391, Label406, Label405, Label404)

        ' Mirrored outputs
        ACC.Tolerance = MapTB("BB", 126, TextBox305, TextBox291, TextBox277, TextBox263, TextBox249, TextBox235, TextBox221, TextBox207, TextBox193, TextBox165, TextBox151, TextBox137)
        ACC.UpperLimit = MapTB("BG", 126, TextBox301, TextBox287, TextBox273, TextBox259, TextBox245, TextBox231, TextBox217, TextBox203, TextBox189, TextBox161, TextBox147, TextBox133)
        ACC.LowerLimit = MapTB("BL", 126, TextBox300, TextBox286, TextBox272, TextBox258, TextBox244, TextBox230, TextBox216, TextBox202, TextBox188, TextBox160, TextBox146, TextBox132)
        ACC.Remarks = MapTB("BQ", 126, TextBox299, TextBox285, TextBox271, TextBox257, TextBox243, TextBox229, TextBox215, TextBox201, TextBox187, TextBox159, TextBox145, TextBox131)

        LockAutoFields(ACC)

        ' ================== live compute wiring ==================
        dcComputeTimer = New Timer() With {.Interval = 300}
        AddHandler dcComputeTimer.Tick, AddressOf OnDcComputeTimerTick
        HookLiveCompute() ' only MV3 -> fires when the "last" value is entered

        ' ================== Excel context (bulk via callbacks) ==================
        ctxDc = New CalRowModule.RowContext With {
            .TemplatePath = "C:\Users\dbneri\Documents\Visual Studio 2010\Projects\ASCal\ASCal\template.xlsx",
            .SheetInputsName = "DataSheet",
            .SheetFormulaName = "DataSheet",
            .hostControls = Me.Controls
        }

        CalRowModule.Initialize(ctxDc)

        ' --- Prime first DC row safely ---
        If DC.MV3 IsNot Nothing AndAlso DC.MV3.Length > 0 Then
            currentGroup = DC
            currentRowIdx = 0
            currentExcelRow = GetRowFromAddr(DC.MV3(0).cell)   ' use DC.MV3 (not the legacy array)
            ctxDc.TargetRow = currentExcelRow
            ctxDc.PreCalculate = Sub(ws) WriteInputsRow(ws, DC, currentRowIdx)
            ctxDc.AfterCalculate = Sub(ws) ReadOutputsRow(ws, DC, currentRowIdx)
            CalRowModule.RecalculateNow(ctxDc)
        End If

    End Sub

    ' ================== live compute helpers ==================
    Private Sub HookLiveCompute()
        If DC IsNot Nothing AndAlso DC.MV3 IsNot Nothing Then
            For Each p In DC.MV3
                If p.tb IsNot Nothing Then AddHandler p.tb.TextChanged, AddressOf OnDcMvChanged
            Next
        End If
        If AC IsNot Nothing AndAlso AC.MV3 IsNot Nothing Then
            For Each p In AC.MV3
                If p.tb IsNot Nothing Then AddHandler p.tb.TextChanged, AddressOf OnAcMvChanged
            Next
        End If
        If RES IsNot Nothing AndAlso RES.MV3 IsNot Nothing Then
            For Each p In RES.MV3
                If p.tb IsNot Nothing Then AddHandler p.tb.TextChanged, AddressOf OnResMvChanged
            Next
        End If
        If DCC IsNot Nothing AndAlso DCC.MV3 IsNot Nothing Then
            For Each p In DCC.MV3
                If p.tb IsNot Nothing Then AddHandler p.tb.TextChanged, AddressOf OnDccMvChanged
            Next
        End If
        'If ACC IsNot Nothing AndAlso ACC.MV3 IsNot Nothing Then
        For Each p In ACC.MV3
            If p.tb IsNot Nothing Then AddHandler p.tb.TextChanged, AddressOf OnAccMvChanged
        Next
        End If
    End Sub

    Private Sub OnDcMvChanged(sender As Object, e As EventArgs)
        Dim tb = TryCast(sender, TextBox)
        Dim rowIdx As Integer = FindRowIndexFromSenderInGroup(DC, tb)
        If rowIdx < 0 Then Exit Sub

        If IsRowComplete(DC, rowIdx) Then
            currentGroup = DC
            currentRowIdx = rowIdx
            currentExcelRow = GetRowFromAddr(DC.MV3(rowIdx).cell)   ' e.g., "AH65" -> 65
            ctxDc.TargetRow = currentExcelRow

            ' Route this calc’s read/write to the DC group & row
            ctxDc.PreCalculate = Sub(ws) WriteInputsRow(ws, DC, currentRowIdx)
            ctxDc.AfterCalculate = Sub(ws) ReadOutputsRow(ws, DC, currentRowIdx)

            dcComputeTimer.Stop()
            dcComputeTimer.Start()
        End If
    End Sub

    Private Sub OnAcMvChanged(sender As Object, e As EventArgs)
        Dim tb = TryCast(sender, TextBox)
        Dim rowIdx As Integer = FindRowIndexFromSenderInGroup(AC, tb)
        If rowIdx < 0 Then Exit Sub

        If IsRowComplete(AC, rowIdx) Then
            currentGroup = AC
            currentRowIdx = rowIdx
            currentExcelRow = GetRowFromAddr(AC.MV3(rowIdx).cell)   ' e.g., "AH76" -> 76
            ctxDc.TargetRow = currentExcelRow

            ' Route this calc’s read/write to the AC group & row
            ctxDc.PreCalculate = Sub(ws) WriteInputsRow(ws, AC, currentRowIdx)
            ctxDc.AfterCalculate = Sub(ws) ReadOutputsRow(ws, AC, currentRowIdx)

            dcComputeTimer.Stop()
            dcComputeTimer.Start()
        End If
    End Sub

    Private Sub OnResMvChanged(sender As Object, e As EventArgs)
        Dim tb = TryCast(sender, TextBox)
        Dim rowIdx As Integer = FindRowIndexFromSenderInGroup(RES, tb)
        If rowIdx < 0 Then Exit Sub

        ' (Optional) if you added visual row highlighting earlier:
        ' HighlightActiveRow(RES, rowIdx)

        If IsRowComplete(RES, rowIdx) Then
            currentGroup = RES
            currentRowIdx = rowIdx
            currentExcelRow = GetRowFromAddr(RES.MV3(rowIdx).cell)   ' e.g., "AH102" -> 102
            ctxDc.TargetRow = currentExcelRow

            ' Route this calc’s write/read to the RES group & row
            ctxDc.PreCalculate = Sub(ws) WriteInputsRow(ws, RES, currentRowIdx)
            ctxDc.AfterCalculate = Sub(ws) ReadOutputsRow(ws, RES, currentRowIdx)

            dcComputeTimer.Stop()
            dcComputeTimer.Start()
        End If
    End Sub

    Private Sub OnDccMvChanged(sender As Object, e As EventArgs)
        Dim tb = TryCast(sender, TextBox)
        Dim rowIdx As Integer = FindRowIndexFromSenderInGroup(DCC, tb)
        If rowIdx < 0 Then Exit Sub

        ' Optional: HighlightActiveRow(DCC, rowIdx)

        If IsRowComplete(DCC, rowIdx) Then
            currentGroup = DCC
            currentRowIdx = rowIdx
            currentExcelRow = GetRowFromAddr(DCC.MV3(rowIdx).cell)   ' e.g., "AH113" -> 113
            ctxDc.TargetRow = currentExcelRow

            ctxDc.PreCalculate = Sub(ws) WriteInputsRow(ws, DCC, currentRowIdx)
            ctxDc.AfterCalculate = Sub(ws) ReadOutputsRow(ws, DCC, currentRowIdx)

            dcComputeTimer.Stop()
            dcComputeTimer.Start()
        End If
    End Sub

    Private Sub OnAccMvChanged(sender As Object, e As EventArgs)
        Dim tb = TryCast(sender, TextBox)
        Dim rowIdx As Integer = FindRowIndexFromSenderInGroup(ACC, tb)
        If rowIdx < 0 Then Exit Sub

        ' Optional: HighlightActiveRow(ACC, rowIdx)

        If IsRowComplete(ACC, rowIdx) Then
            currentGroup = ACC
            currentRowIdx = rowIdx
            currentExcelRow = GetRowFromAddr(ACC.MV3(rowIdx).cell)   ' e.g., "AH126" -> 126
            ctxDc.TargetRow = currentExcelRow

            ctxDc.PreCalculate = Sub(ws) WriteInputsRow(ws, ACC, currentRowIdx)
            ctxDc.AfterCalculate = Sub(ws) ReadOutputsRow(ws, ACC, currentRowIdx)

            dcComputeTimer.Stop()
            dcComputeTimer.Start()
        End If
    End Sub

    Private Function FindRowIndexFromSenderInGroup(g As ParamGroup, tb As TextBox) As Integer
        If g Is Nothing OrElse tb Is Nothing OrElse g.MV1 Is Nothing Then Return -1
        For i = 0 To g.MV1.Length - 1
            If (g.MV1 IsNot Nothing AndAlso i < g.MV1.Length AndAlso g.MV1(i).tb Is tb) _
        OrElse (g.MV2 IsNot Nothing AndAlso i < g.MV2.Length AndAlso g.MV2(i).tb Is tb) _
        OrElse (g.MV3 IsNot Nothing AndAlso i < g.MV3.Length AndAlso g.MV3(i).tb Is tb) Then
                Return i
            End If
        Next
        Return -1
    End Function

    Private Function IsRowComplete(g As ParamGroup, i As Integer) As Boolean
        If g Is Nothing OrElse i < 0 Then Return False
        If g.MV1 Is Nothing OrElse g.MV2 Is Nothing OrElse g.MV3 Is Nothing Then Return False
        If i >= g.MV1.Length OrElse i >= g.MV2.Length OrElse i >= g.MV3.Length Then Return False
        Dim t1 = g.MV1(i).tb.Text, t2 = g.MV2(i).tb.Text, t3 = g.MV3(i).tb.Text
        Return t1 IsNot Nothing AndAlso t1.Trim().Length > 0 AndAlso
           t2 IsNot Nothing AndAlso t2.Trim().Length > 0 AndAlso
           t3 IsNot Nothing AndAlso t3.Trim().Length > 0
    End Function

    Private Sub OnDcComputeTimerTick(sender As Object, e As EventArgs)
        dcComputeTimer.Stop()
        If currentExcelRow > 0 Then ctxDc.TargetRow = currentExcelRow
        ' triggers: PreCalculate -> (targeted) Calculate -> AfterCalculate
        CalRowModule.RecalculateNow(ctxDc)
    End Sub

    Private Sub calibratingResult_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        If ctxDc IsNot Nothing Then CalRowModule.SaveToExcel(ctxDc)
    End Sub

    ' ================== Excel <-> UI helpers ==================

    ' Quick: read/write via Range("A1") addressing to avoid column math
    Private Sub WriteCell(ws As Object, addr As String, value As String)
        Dim cell = CallByName(ws, "Range", CallType.Get, addr)
        CallByName(cell, "Value", CallType.Let, value)
    End Sub

    Private Function ReadCell(ws As Object, addr As String) As String
        Dim cell = CallByName(ws, "Range", CallType.Get, addr)
        Return CStr(If(CallByName(cell, "Text", CallType.Get), ""))
    End Function

    Private Function GetRowFromAddr(addr As String) As Integer
        ' split letters/digits without LINQ
        Dim i As Integer = 0
        While i < addr.Length AndAlso Char.IsLetter(addr(i))
            i += 1
        End While
        Return Integer.Parse(addr.Substring(i))
    End Function

    Private Sub ApplyPassFailColor(tb As TextBox)
        If tb Is Nothing Then Exit Sub
        Dim val = If(tb.Text, "").Trim().ToUpperInvariant()

        Select Case val
            Case "PASS"
                tb.BackColor = System.Drawing.Color.FromArgb(198, 239, 206) ' soft green
                tb.ForeColor = System.Drawing.Color.Black
            Case "FAIL"
                tb.BackColor = System.Drawing.Color.FromArgb(255, 199, 206) ' soft red
                tb.ForeColor = System.Drawing.Color.Black
            Case Else
                ' restore your normal read-only look
                tb.BackColor = SystemColors.ControlLight
                tb.ForeColor = SystemColors.WindowText
        End Select
    End Sub

End Class