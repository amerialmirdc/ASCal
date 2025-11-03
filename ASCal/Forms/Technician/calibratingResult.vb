Option Strict Off

Imports System
Imports System.ComponentModel
Imports System.Drawing.Imaging
Imports System.Globalization
Imports System.IO
Imports System.IO.Compression
Imports System.IO.Packaging
Imports System.IO.Ports
Imports System.Linq
Imports System.Net.Security
Imports System.Reflection
Imports System.Runtime.InteropServices
Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Xml
Imports System.Xml.Linq
Imports AForge
Imports AForge.Video
Imports AForge.Video.DirectShow
Imports Drawing = System.Drawing
Imports WinForms = System.Windows.Forms

Public Class calibratingResult
    Dim wrongrangeparameter As Integer = 0
    Dim looping As Integer = 1 'total number ng pagkuha ng mga reading
    Dim stringList As New List(Of String)
    Dim loopdelaythreadsleep As Integer = 0
    Dim dec As Integer = 33
    Dim malingreading As Integer = 0
    Dim wireresistance As Decimal = 0
    Dim getwireresistance As Integer = 0

#Region "Fields & Excel context"

    ' -------------------------------
    ' Handles navigation buttons (logo, logout, dashboard)
    ' -------------------------------
    Private Sub HandleNavClick(sender As Object, e As EventArgs) Handles logoBtn.Click, logoutBtn.Click, jobDashBtn.Click
        Select Case True
            Case sender Is logoBtn
                landingPageTechnician.Show()
                Me.Close()
            Case sender Is logoutBtn
                login.Show()
                Me.Close()
            Case sender Is jobDashBtn
                jobDashTech.Show()
                Me.Close()
        End Select
    End Sub

    Private ctxDc As CalRowModule.RowContext

    ' Parameter group holder
    Private Class ParamGroup

        ' Row-descriptor fields using the column names
        Public COL_FUNCTION As (lbl As WinForms.Label, cell As String)()

        Public RangeLabel As (lbl As WinForms.Label, cell As String)()
        Public Nominal As (lbl As WinForms.Label, cell As String)()
        Public Unit As (lbl As WinForms.Label, cell As String)()
        Public Frequency As (lbl As WinForms.Label, cell As String)()
        Public FreqUnit As (lbl As WinForms.Label, cell As String)()

        Public MV1 As (tb As WinForms.TextBox, cell As String)()
        Public MV2 As (tb As WinForms.TextBox, cell As String)()
        Public MV3 As (tb As WinForms.TextBox, cell As String)()
        Public Average As (lbl As WinForms.Label, cell As String)()
        Public [Error] As (lbl As WinForms.Label, cell As String)()
        Public FinalUncDecl As (lbl As WinForms.Label, cell As String)()
        Public Tolerance As (tb As WinForms.TextBox, cell As String)()
        Public UpperLimit As (tb As WinForms.TextBox, cell As String)()
        Public LowerLimit As (tb As WinForms.TextBox, cell As String)()
        Public Remarks As (tb As WinForms.TextBox, cell As String)()

        Public TemplateRowCount As Integer   ' how many rows the sheet actually has (by Column A scan)

        ' Identity / grouping
        Public SheetKey As String      ' actual dictionary key (e.g., "ACV", "ACV_1")

        Public SheetBase As String     ' base name without numeric suffix (e.g., "ACV")
        Public SheetIndex As Integer   ' 0 for first, 1 for second, etc.
        Public SheetId As String       ' e.g., "acv:0" – stable, easy to group

    End Class

    Private Groups As New Dictionary(Of String, ParamGroup)(StringComparer.OrdinalIgnoreCase)
    ' ========= GROUP STORAGE =========

    Private currentGroup As ParamGroup = Nothing
    Private currentRowIdx As Integer = -1
    Private currentExcelRow As Integer = -1
    Private reportGenerated As Boolean = False

#End Region

#Region "Inbound properties from calibrate.vb (headers & context)"

    Public Property JobId As Integer
    Public Property WorkOrderNumber As String
    Public Property CompanyName As String
    Public Property CompanyAddress As String
    Public Property Model As String
    Public Property Manufacturer As String
    Public Property Description As String
    Public Property TechnicianInitials As String
    Public Property TechnicianName As String
    Public Property CalibrationType As String
    Public Property SpecificSite As String
    Public Property SerialNumber As String
    Public Property SelectedParameters As List(Of String)
    Public Property ActiveCategories As List(Of String)

    ' Left header details
    Public Property Range As String                  ' K17

    Public Property Readability As String            ' K19
    Public Property PrevSesCalCert As String         ' K21

    ' Right-side header block
    Public Property ReceivedDate As String           ' AL9

    Public Property CalibrationDate As String        ' AL11
    Public Property OptionsInstalled As String       ' AL13
    Public Property CustomerPO As String             ' AL15
    Public Property AssetNumber As String            ' AL17
    Public Property AccuracyHeader As String         ' AL19
    Public Property PreviousTechnician As String     ' AL21

    ' Environmental conditions
    Public Property TempStart As String              ' K41

    Public Property TempEnd As String                ' K42
    Public Property HumidityStart As String          ' T41
    Public Property HumidityEnd As String            ' T42

    ' Optional: reference / accessory fields
    Public Property RefDesc1 As String

    Public Property RefSN1 As String
    Public Property RefCalRef1 As String
    Public Property RefDue1 As String
    Public Property RefDesc2 As String
    Public Property RefSN2 As String
    Public Property RefCalRef2 As String
    Public Property RefDue2 As String

    Public Property AccDesc1 As String
    Public Property AccSN1 As String
    Public Property AccCalBrand1 As String
    Public Property AccModel1 As String
    Public Property AccDesc2 As String
    Public Property AccSN2 As String
    Public Property AccCalBrand2 As String
    Public Property AccModel2 As String
    Public Property calMathod As String

#End Region

#Region "Core compute + OCR hooks + TEMP stubs"

    Private Function ReorderTupleTB(arr As (tb As TextBox, cell As String)(), order As Integer()) _
        As (tb As TextBox, cell As String)()
        If arr Is Nothing Then Return Nothing
        Dim out(arr.Length - 1) As (TextBox, String)
        For i = 0 To Math.Min(arr.Length, order.Length) - 1 : out(i) = arr(order(i)) : Next
        Return out
    End Function

    Private Function ReorderTupleLBL(arr As (lbl As Label, cell As String)(), order As Integer()) _
    As (lbl As Label, cell As String)()
        If arr Is Nothing Then Return Nothing
        Dim out(arr.Length - 1) As (Label, String)
        For i = 0 To Math.Min(arr.Length, order.Length) - 1
            out(i) = arr(order(i))
        Next
        Return out
    End Function

    Private Function GetMappedLabel(arr As (lbl As Label, cell As String)(), idx As Integer) As Label
        If arr Is Nothing OrElse idx < 0 OrElse idx >= arr.Length Then Return Nothing
        Return arr(idx).lbl
    End Function

    ' Pick the best “anchor” control for a given row by scanning ALL columns
    Private Function FirstControlOfRow(g As ParamGroup, i As Integer) As WinForms.Control
        Dim best As WinForms.Control = Nothing
        Dim bestTop As Integer = Integer.MaxValue

        Dim tryCtrl = Sub(c As WinForms.Control)
                          If c IsNot Nothing AndAlso c.Top < bestTop Then
                              best = c : bestTop = c.Top
                          End If
                      End Sub

        ' Descriptor labels (left side) – new
        If g.RangeLabel IsNot Nothing AndAlso i < g.RangeLabel.Length Then tryCtrl(g.RangeLabel(i).lbl)
        If g.COL_FUNCTION IsNot Nothing AndAlso i < g.COL_FUNCTION.Length Then tryCtrl(g.COL_FUNCTION(i).lbl)
        If g.Nominal IsNot Nothing AndAlso i < g.Nominal.Length Then tryCtrl(g.Nominal(i).lbl)
        If g.Unit IsNot Nothing AndAlso i < g.Unit.Length Then tryCtrl(g.Unit(i).lbl)
        If g.Frequency IsNot Nothing AndAlso i < g.Frequency.Length Then tryCtrl(g.Frequency(i).lbl)
        If g.FreqUnit IsNot Nothing AndAlso i < g.FreqUnit.Length Then tryCtrl(g.FreqUnit(i).lbl)

        ' Inputs
        If g.MV1 IsNot Nothing AndAlso i < g.MV1.Length Then tryCtrl(g.MV1(i).tb)
        If g.MV2 IsNot Nothing AndAlso i < g.MV2.Length Then tryCtrl(g.MV2(i).tb)
        If g.MV3 IsNot Nothing AndAlso i < g.MV3.Length Then tryCtrl(g.MV3(i).tb)

        ' Result labels
        If g.Average IsNot Nothing AndAlso i < g.Average.Length Then tryCtrl(g.Average(i).lbl)
        If g.Error IsNot Nothing AndAlso i < g.Error.Length Then tryCtrl(g.Error(i).lbl)
        If g.FinalUncDecl IsNot Nothing AndAlso i < g.FinalUncDecl.Length Then tryCtrl(g.FinalUncDecl(i).lbl)

        ' Limits / remarks
        If g.Tolerance IsNot Nothing AndAlso i < g.Tolerance.Length Then tryCtrl(g.Tolerance(i).tb)
        If g.UpperLimit IsNot Nothing AndAlso i < g.UpperLimit.Length Then tryCtrl(g.UpperLimit(i).tb)
        If g.LowerLimit IsNot Nothing AndAlso i < g.LowerLimit.Length Then tryCtrl(g.LowerLimit(i).tb)
        If g.Remarks IsNot Nothing AndAlso i < g.Remarks.Length Then tryCtrl(g.Remarks(i).tb)

        Return best ' may be Nothing (caller handles)
    End Function

    Private Sub NormalizeGroupOrderByTop(g As ParamGroup)
        If g Is Nothing Then Exit Sub

        ' Define rows by inputs (prefer MV1)
        Dim n As Integer = 0
        If g.MV1 IsNot Nothing Then n = Math.Max(n, g.MV1.Length)
        If g.MV2 IsNot Nothing Then n = Math.Max(n, g.MV2.Length)
        If g.MV3 IsNot Nothing Then n = Math.Max(n, g.MV3.Length)
        If n <= 1 Then Exit Sub

        ' Order by MV1.Top; if missing, fall back to MV2→MV3→FirstControlOfRow
        Dim pairs As New List(Of Tuple(Of Integer, Integer))()
        For i = 0 To n - 1
            Dim topVal = Integer.MaxValue
            If g.MV1 IsNot Nothing AndAlso i < g.MV1.Length AndAlso g.MV1(i).tb IsNot Nothing Then
                topVal = g.MV1(i).tb.Top
            ElseIf g.MV2 IsNot Nothing AndAlso i < g.MV2.Length AndAlso g.MV2(i).tb IsNot Nothing Then
                topVal = g.MV2(i).tb.Top
            ElseIf g.MV3 IsNot Nothing AndAlso i < g.MV3.Length AndAlso g.MV3(i).tb IsNot Nothing Then
                topVal = g.MV3(i).tb.Top
            Else
                Dim c = FirstControlOfRow(g, i)
                If c IsNot Nothing Then topVal = c.Top
            End If
            pairs.Add(Tuple.Create(i, topVal))
        Next
        Dim order = pairs.OrderBy(Function(t) t.Item2).Select(Function(t) t.Item1).ToArray()

        ' Reorder inputs/outputs/labels together
        g.MV1 = ReorderTupleTB(g.MV1, order)
        g.MV2 = ReorderTupleTB(g.MV2, order)
        g.MV3 = ReorderTupleTB(g.MV3, order)
        g.Average = ReorderTupleLBL(g.Average, order)
        g.[Error] = ReorderTupleLBL(g.[Error], order)
        g.FinalUncDecl = ReorderTupleLBL(g.FinalUncDecl, order)
        g.Tolerance = ReorderTupleTB(g.Tolerance, order)
        g.UpperLimit = ReorderTupleTB(g.UpperLimit, order)
        g.LowerLimit = ReorderTupleTB(g.LowerLimit, order)
        g.Remarks = ReorderTupleTB(g.Remarks, order)

        ' Reorder new left labels (tuple)
        g.COL_FUNCTION = ReorderTupleLBL(g.COL_FUNCTION, order)
        g.RangeLabel = ReorderTupleLBL(g.RangeLabel, order)
        g.Nominal = ReorderTupleLBL(g.Nominal, order)
        g.Unit = ReorderTupleLBL(g.Unit, order)
        g.Frequency = ReorderTupleLBL(g.Frequency, order)
        g.FreqUnit = ReorderTupleLBL(g.FreqUnit, order)
    End Sub

    ' Accepts 1..N row indices. Single-row → compute now; multi-row → timer.
    ' Kick compute for exactly one row and rearm the tick to finish the UI update.
    Private Sub StartRowCompute(g As ParamGroup, rowIdx As Integer)
        If g Is Nothing OrElse rowIdx < 0 Then Exit Sub

        ' Resolve Excel row from any MV* mapped address for this row
        Dim addr As String = Nothing
        If g.MV3 IsNot Nothing AndAlso rowIdx < g.MV3.Length Then addr = g.MV3(rowIdx).cell
        If String.IsNullOrWhiteSpace(addr) AndAlso g.MV2 IsNot Nothing AndAlso rowIdx < g.MV2.Length Then addr = g.MV2(rowIdx).cell
        If String.IsNullOrWhiteSpace(addr) AndAlso g.MV1 IsNot Nothing AndAlso rowIdx < g.MV1.Length Then addr = g.MV1(rowIdx).cell
        If String.IsNullOrWhiteSpace(addr) Then Exit Sub

        Dim excelRow As Integer = GetRowFromAddr(addr)
        If excelRow <= 0 Then Exit Sub

        Try : SetSheetsIfChanged(ctxDc, g.SheetKey) : Catch : End Try
        ctxDc.TargetRow = excelRow

        ' Wire Pre/After for this row and compute now
        ctxDc.PreCalculate = Sub(ws) WriteInputsRow(ws, g, rowIdx)
        ctxDc.AfterCalculate = Sub(ws) ReadOutputsRow(ws, g, rowIdx)
        CalRowModule.RecalculateNow(ctxDc)
    End Sub

    ' Add inside calibratingResult (e.g., near ApplyCategoriesAndSelection)
    Private Sub SetLabelTextIfPresent(lb As Label, value As String)
        If lb Is Nothing Then Exit Sub
        lb.Text = value
        lb.Visible = True
    End Sub

    ' Convenience: use the current row and fill the first empty MV cell
    Private Sub AutoApplyReadingToCurrentRow(reading As String)
        If String.IsNullOrWhiteSpace(reading) Then Exit Sub

        ' Safety: mappings must exist

        ' Pick group if not set yet (use parameter + optional DMMmode)
        If currentGroup Is Nothing Then
            Dim p As String = If(DMMtxtparameter.Text, "").Trim().ToUpperInvariant()   ' "V", "A", "Ω"/"OHM"
            Dim mode As String = ""
            Dim mCtrl = Me.Controls.Find("DMMmode", True).FirstOrDefault()
            If TypeOf mCtrl Is TextBox Then mode = DirectCast(mCtrl, TextBox).Text.Trim().ToUpperInvariant()   ' "AC" or "DC"

            Select Case p
                Case Else
                    ' Fallback: first visible group that has MV1 rows
                    For Each g In Groups.Values
                        If g IsNot Nothing AndAlso g.MV1 IsNot Nothing Then
                            For i = 0 To g.MV1.Length - 1
                                If g.MV1(i).tb IsNot Nothing AndAlso g.MV1(i).tb.Visible Then
                                    currentGroup = g : currentRowIdx = i : Exit For
                                End If
                            Next
                        End If
                        If currentGroup IsNot Nothing Then Exit For
                    Next
            End Select
        End If
        If currentGroup Is Nothing Then Exit Sub

        ' Choose row if none yet (first visible)
        If currentRowIdx < 0 Then
            currentRowIdx = 0
            If currentGroup.MV1 IsNot Nothing Then
                For i = 0 To currentGroup.MV1.Length - 1
                    Dim tb = currentGroup.MV1(i).tb
                    If tb IsNot Nothing AndAlso tb.Visible Then currentRowIdx = i : Exit For
                Next
            End If
        End If

        ' --- Write to the first empty MV in the row ---
        Dim wrote As Boolean = False
        If currentGroup.MV1 IsNot Nothing AndAlso currentRowIdx < currentGroup.MV1.Length Then
            Dim tb = currentGroup.MV1(currentRowIdx).tb
            ' Remove the IsEmptyMv function calls and handle them directly:
            If tb IsNot Nothing AndAlso String.IsNullOrWhiteSpace(tb.Text) Then
                tb.Text = reading
                wrote = True
            End If
        End If
        If Not wrote AndAlso currentGroup.MV2 IsNot Nothing AndAlso currentRowIdx < currentGroup.MV2.Length Then
            Dim tb = currentGroup.MV2(currentRowIdx).tb
            ' Remove the IsEmptyMv function calls and handle them directly:
            If tb IsNot Nothing AndAlso String.IsNullOrWhiteSpace(tb.Text) Then
                tb.Text = reading
                wrote = True
            End If
        End If
        If Not wrote AndAlso currentGroup.MV3 IsNot Nothing AndAlso currentRowIdx < currentGroup.MV3.Length Then
            Dim tb = currentGroup.MV3(currentRowIdx).tb
            ' Remove the IsEmptyMv function calls and handle     them directly:
            If tb IsNot Nothing AndAlso String.IsNullOrWhiteSpace(tb.Text) Then
                tb.Text = reading
                wrote = True
            End If
        End If

        ' If row complete → compute → advance pointer to next visible row
        If IsRowComplete(currentGroup, currentRowIdx) Then
            currentExcelRow = GetRowFromAddr(currentGroup.MV3(currentRowIdx).cell)
            ctxDc.TargetRow = currentExcelRow
            StartRowCompute(currentGroup, currentRowIdx)

            ' Find the next visible row
            If currentGroup.MV1 IsNot Nothing Then
                For i = currentRowIdx + 1 To currentGroup.MV1.Length - 1
                    Dim tb = currentGroup.MV1(i).tb
                    If tb IsNot Nothing AndAlso tb.Visible Then
                        currentRowIdx = i
                        ' Move focus to the first visible textbox in the next row (MV1)
                        Dim nextControl = currentGroup.MV1(currentRowIdx).tb
                        If nextControl IsNot Nothing Then
                            nextControl.Focus()
                            nextControl.SelectAll()
                        End If
                        Exit Sub
                    End If
                Next
            End If
        End If
    End Sub

    ' --- needed because TEMP regions are commented out ---
    ' keeps bulk textbox updates from re-triggering compute
    Private isBulkUpdating As Boolean = False

    ' nominal sequencer targets placeholder (ApplyActiveCategories still touches this)
    Private nomSeqTargets As List(Of (tb As TextBox, value As String)) = Nothing

    ' overall run stopwatch (used by OnDcComputeTimerTick timing summary)
    'Private runStopwatch As System.Diagnostics.Stopwatch = Nothing

#End Region

#Region "Load / Close"

    Public Property UseSerialUI As Boolean = True
    Dim myPort As Array  'COM Ports detected on the system will be stored here

    Delegate Sub SetTextCallback(ByVal [text] As String) 'Added to prevent threading errors during receiveing of data

    Dim sapi

    Private Sub calibratingResult_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        myPort = IO.Ports.SerialPort.GetPortNames() 'Get all com ports available
        CmbBaud.Items.Add(9600)     'Populate the cmbBaud Combo box to common baud rates used

        For i = 0 To UBound(myPort)
            CmbPort.Items.Add(myPort(i))
        Next
        CmbPort.Text = CmbPort.Items.Item(0)    'Set cmbPort text to the first COM port detected
        CmbBaud.Text = CmbBaud.Items.Item(0)    'Set cmbBaud text to the first Baud rate on the list
        BtnDisconnect.Enabled = False           'Initially Disconnect Button is Disabled
        ' ================= WINDOW =================
        Me.StartPosition = FormStartPosition.Manual

        Me.MaximumSize = New Size(0, 0)
        Me.MinimumSize = New Size(0, 0)
        Me.Bounds = Screen.FromControl(Me).WorkingArea

        ' ================= SERIAL UI =================
        'BtnConnect.PerformClick()
        'Thread.Sleep(500)
        'SerialPort1.Write("OREMOTEX")
        ' ================= CAMERA =================
        Try
            If videoSource IsNot Nothing Then
                RemoveHandler videoSource.NewFrame, AddressOf Video_NewFrame
                If videoSource.IsRunning Then
                    videoSource.SignalToStop()
                End If
            End If
        Catch
        End Try
        Dim cam = CreatePreferredCamera()
        If cam IsNot Nothing Then
            videoSource = cam
            AddHandler videoSource.NewFrame, AddressOf Video_NewFrame
            videoSource.Start()
            ' keep camera visible above preview (assumes your PictureBox name; adjust if different)
            Dim camCtl = Me.Controls.Find("pbCamera", True).FirstOrDefault()
            If camCtl IsNot Nothing Then
                camCtl.Visible = True
                camCtl.BringToFront()
            End If
        End If

        Dim snippingToolProcesses As String() = {"SnippingTool", "SnipAndSketch"}

        For Each procName In snippingToolProcesses
            Dim processes As Process() = Process.GetProcessesByName(procName)

            For Each proc In processes
                Try
                    proc.Kill()
                    proc.WaitForExit()
                    'MessageBox.Show($"{proc.ProcessName} closed successfully.")
                Catch ex As Exception
                    'MessageBox.Show($"Failed to close {proc.ProcessName}: {ex.Message}")
                End Try
            Next
        Next

        ' ================= TEMPLATE =================
        Dim appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        Dim tmplDir = IO.Path.Combine(appData, "DMMCal", "Templates")
        If Not IO.Directory.Exists(tmplDir) Then IO.Directory.CreateDirectory(tmplDir)

        Dim Slug = Function(s As String) As String
                       s = If(s, "").Trim()
                       If s.Length = 0 Then Return "NA"
                       For Each ch In IO.Path.GetInvalidFileNameChars()
                           s = s.Replace(ch, "_"c)
                       Next
                       Return s
                   End Function

        Dim m = If(Model, "").Trim()
        Dim mf = If(Manufacturer, "").Trim()
        Dim ds = If(Description, "").Trim()
        Dim p3 = IO.Path.Combine(tmplDir, $"{Slug(m)}__{Slug(mf)}__{Slug(ds)}.xlsx")
        Dim p2 = IO.Path.Combine(tmplDir, $"{Slug(m)}__{Slug(mf)}.xlsx")
        Dim p1 = IO.Path.Combine(tmplDir, $"{Slug(m)}.xlsx")

        Dim template As String = ""
        If IO.File.Exists(p3) Then
            template = p3
        ElseIf IO.File.Exists(p2) Then
            template = p2
        ElseIf IO.File.Exists(p1) Then
            template = p1
        End If

        If String.IsNullOrWhiteSpace(template) OrElse Not IO.File.Exists(template) Then
            MessageBox.Show("Missing Excel template for this model." & Environment.NewLine &
                    $"Looked in: {tmplDir}", "Template Not Found",
                    MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        Dim unique = $"{DateTime.UtcNow:yyyyMMdd_HHmmssfff}_{Guid.NewGuid:N}"
        Dim workingCopy = IO.Path.Combine(IO.Path.GetTempPath(),
    $"ASCal_{NormalizeFile(WorkOrderNumber)}_{NormalizeFile(SerialNumber)}_{unique}.xlsx")

        Try
            If IO.File.Exists(workingCopy) Then IO.File.Delete(workingCopy)
            If IO.File.Exists(workingCopy) Then IO.File.Delete(workingCopy)
            IO.File.Copy(template, workingCopy, True)
        Catch ex As Exception
            MessageBox.Show("Unable to prepare Excel template: " & ex.Message, "Template Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End Try

        ' Excel context (keep this)
        ctxDc = New CalRowModule.RowContext With {
        .TemplatePath = workingCopy,
        .SheetInputsName = "DataSheet",
        .SheetFormulaName = "DataSheet",
        .hostControls = Me.Controls}

        CalRowModule.Initialize(ctxDc)
        If ctxDc Is Nothing Then
            MessageBox.Show("Failed to initialize Excel context.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If
        ' ====== SHEET DISCOVERY & MAPPING (include ALL sheets) ======
        'Groups.Clear()

        Dim allSheets = GetWorksheetNamesFromXlsx(ctxDc.TemplatePath)
        If allSheets Is Nothing OrElse allSheets.Count = 0 Then
            allSheets = New List(Of String) From {ctxDc.SheetInputsName}
        End If

        Dim dataSheets As New List(Of String)

        CalRowModule.WithWorksheet(ctxDc, Sub(ws)
                                              For Each sh In allSheets
                                                  Dim looksData As Boolean = False
                                                  Try
                                                      ctxDc.SheetInputsName = sh
                                                      ctxDc.SheetFormulaName = sh

                                                      Dim blankStreak As Integer = 0
                                                      For r As Integer = 2 To 50
                                                          Dim aVal As String = CalRowModule.ReadCell(ws, "A" & r)
                                                          Dim cVal As String = CalRowModule.ReadCell(ws, "C" & r)
                                                          If String.IsNullOrWhiteSpace(aVal) AndAlso String.IsNullOrWhiteSpace(cVal) Then
                                                              blankStreak += 1
                                                              If blankStreak >= 20 Then Exit For
                                                          Else
                                                              looksData = True
                                                              Exit For
                                                          End If
                                                      Next
                                                  Catch
                                                      looksData = False
                                                  End Try
                                                  If looksData Then dataSheets.Add(sh)
                                              Next
                                          End Sub)

        If dataSheets.Count = 0 Then dataSheets.Add(allSheets(0))  ' fallback

        ' ================= POPULATE namesArr =================
        Dim namesArr As New List(Of String)()
        CalRowModule.WithWorksheet(ctxDc, Sub(ws)
                                              ' choose which sheets to scan; dataSheets has your detected “data-looking” tabs
                                              Dim scanSheets = If(dataSheets IsNot Nothing AndAlso dataSheets.Count > 0, dataSheets, allSheets)

                                              ' columns you already touch elsewhere (A..F for left; G..Q inputs/limits; J,K,AI outputs)
                                              Dim cols As String() = {"A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "N", "O", "P", "Q", "AI"}

                                              For Each sh In scanSheets
                                                  Try
                                                      ' hop to sheet
                                                      ctxDc.SheetInputsName = sh
                                                      ctxDc.SheetFormulaName = sh

                                                      ' walk rows starting at 2 until we hit a long blank streak
                                                      Dim blankStreak As Integer = 0
                                                      For r As Integer = 2 To 1000
                                                          Dim anyOnRow As Boolean = False

                                                          For Each col In cols
                                                              Dim addr = col & r.ToString()
                                                              Dim v As String = CalRowModule.ReadCell(ws, addr)
                                                              If Not String.IsNullOrWhiteSpace(v) Then
                                                                  namesArr.Add(sh & "|" & addr & "|" & v)
                                                                  anyOnRow = True
                                                              End If
                                                          Next

                                                          If anyOnRow Then
                                                              blankStreak = 0
                                                          Else
                                                              blankStreak += 1
                                                              If blankStreak >= 50 Then Exit For ' stop scan if long blank tail
                                                          End If
                                                      Next
                                                  Catch
                                                      ' ignore sheet read issues and continue
                                                  End Try
                                              Next
                                          End Sub)

        ' Convert to array if you still need it
        Dim namesArrArray As String() = namesArr.ToArray()

        ' Continue with the rest of your logic
        For Each sh In allSheets
            ctxDc.SheetInputsName = sh
            ctxDc.SheetFormulaName = sh
            Me.InitMappings()
        Next

        ctxDc.SheetInputsName = dataSheets(0)
        ctxDc.SheetFormulaName = dataSheets(0)

        If Groups Is Nothing Then Groups = New Dictionary(Of String, ParamGroup)(StringComparer.OrdinalIgnoreCase)
        If Groups.Count = 0 Then
            MessageBox.Show("No parameter groups were mapped by InitMappings()." & Environment.NewLine &
            "Ensure InitMappings fills Groups(<any key you want>) directly.",
            "Mappings Missing", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        For Each g In Groups.Values
            NormalizeGroupOrderByTop(g)
        Next

        ' ================= PREVIEW PANEL (populate) =================
        PopulatePreview()

        ApplyCategoriesAndSelection()

        HookLiveCompute()
    End Sub

    Private Sub BtnConnect_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles BtnConnect.Click
        SerialPort1.PortName = CmbPort.Text         'Set SerialPort1 to the selected COM port at startup
        SerialPort1.BaudRate = CmbBaud.Text         'Set Baud rate to the selected value on

        'Other Serial Port Property
        SerialPort1.Parity = IO.Ports.Parity.None
        SerialPort1.StopBits = IO.Ports.StopBits.One
        SerialPort1.DataBits = 8            'Open our serial port
        SerialPort1.Open()

        BtnConnect.Enabled = False          'Disable Connect button
        BtnDisconnect.Enabled = True        'and Enable Disconnect button

    End Sub

    Private Sub BtnDisconnect_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles BtnDisconnect.Click
        SerialPort1.Close()             'Close our Serial Port

        BtnConnect.Enabled = True
        BtnDisconnect.Enabled = False
    End Sub

    Private Sub BtnSend_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles BtnSend.Click
        SerialPort1.Write(txtTransmit.Text) 'The text contained in the txtText will be sent to the serial port as ascii
        'plus the carriage return (Enter Key) the carriage return can be ommitted if the other end does not need it
    End Sub

    Private Sub SerialPort1_DataReceived(ByVal sender As Object, ByVal e As System.IO.Ports.SerialDataReceivedEventArgs) Handles SerialPort1.DataReceived
        ReceivedText(SerialPort1.ReadExisting())    'Automatically called every time a data is received at the serialPort
    End Sub

    Private Sub ReceivedText(ByVal [text] As String)
        'compares the ID of the creating Thread to the ID of the calling Thread
        If Me.rtbReceived.InvokeRequired Then
            Dim x As New SetTextCallback(AddressOf ReceivedText)
            Me.Invoke(x, New Object() {(text)})
        Else
            Me.rtbReceived.Text &= [text]
        End If
    End Sub

    Private Sub CmbPort_SelectedIndexChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles CmbPort.SelectedIndexChanged
        If SerialPort1.IsOpen = False Then
            SerialPort1.PortName = CmbPort.Text         'pop a message box to user if he is changing ports
        Else                                                                               'without disconnecting first.
            MsgBox(”Valid only if port is Closed”, vbCritical)
        End If
    End Sub

    Private Sub CmbBaud_SelectedIndexChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles CmbBaud.SelectedIndexChanged
        If SerialPort1.IsOpen = False Then
            SerialPort1.BaudRate = CmbBaud.Text         'pop a message box to user if he is changing baud rate
        Else                                                                                'without disconnecting first.
            MsgBox(”Valid only if port is Closed”, vbCritical)
        End If
    End Sub

    ' --- Render preview UI into a specific FlowLayoutPanel ---
    Private Sub PopulatePreview(Optional target As FlowLayoutPanel = Nothing)

        ' ---------- Resolve target ----------
        Dim fl As FlowLayoutPanel = target
        If fl Is Nothing Then
            fl = TryCast(Me.Controls.Find("previewcalibrating", True).FirstOrDefault(), FlowLayoutPanel)
            If fl Is Nothing Then Exit Sub
            fl.Visible = True
        End If

        fl.SuspendLayout()
        fl.FlowDirection = FlowDirection.TopDown
        fl.WrapContents = False
        Dim oldScroll = fl.AutoScroll
        fl.AutoScroll = False
        fl.Controls.Clear()

        ' ---------- Available width ----------
        Dim availW As Integer = fl.DisplayRectangle.Width
        If availW <= 0 Then availW = If(fl.Parent IsNot Nothing, fl.Parent.ClientSize.Width, 800)
        availW = Math.Max(200, availW - fl.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth)

        ' ---------- Column maps (case-insensitive) ----------
        Dim colLeft As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        Dim colWidth As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

        Dim GetW As Func(Of String, Integer) =
        Function(k As String)
            Dim v As Integer
            If colWidth.TryGetValue(k, v) Then Return v
            Return 80
        End Function

        Dim GetL As Func(Of String, Integer) =
        Function(k As String)
            Dim v As Integer
            If colLeft.TryGetValue(k, v) Then Return v
            Return 0
        End Function

        ' ---------- Header panel ----------
        Dim hdr As New Panel() With {.Height = 22, .Width = availW}
        Dim x As Integer = 0

        Dim addHdr =
        Sub(text As String, left As Integer, width As Integer)
            Dim lbl As New Label() With {
                .AutoSize = False, .Text = text, .Left = left, .Top = 2,
                .Width = width, .Font = New Font(Me.Font, FontStyle.Bold)
            }
            hdr.Controls.Add(lbl)
        End Sub

        Dim addColDynamic =
        Sub(key As String, values As IEnumerable(Of String), minW As Integer, pad As Integer)
            Dim seq As IEnumerable(Of String) = If(values, Enumerable.Empty(Of String)())
            Dim maxWidth As Integer =
                seq.Select(Function(v) If(v IsNot Nothing, TextRenderer.MeasureText(v, Me.Font).Width, 0)).
                    DefaultIfEmpty(0).Max()
            Dim w As Integer = Math.Max(minW, maxWidth + pad)
            addHdr(key, x, w)
            colLeft(key) = x
            colWidth(key) = w
            x += w
        End Sub

        Dim addColFixed =
        Sub(key As String, width As Integer)
            addHdr(key, x, width)
            colLeft(key) = x
            colWidth(key) = width
            x += width
        End Sub

        ' ---------- Use insertion order (no sorting) ----------
        Dim ordered As List(Of ParamGroup) = Groups.Values.ToList()

        ' ---------- Build columns from the FIRST available group ----------
        For Each g In ordered
            If g Is Nothing Then Continue For
            addColDynamic("Function", If(g.COL_FUNCTION IsNot Nothing, g.COL_FUNCTION.Select(Function(t) If(t.lbl Is Nothing, "", t.lbl.Text)), Nothing), 60, 10)
            addColDynamic("RangeLabel", If(g.RangeLabel IsNot Nothing, g.RangeLabel.Select(Function(t) If(t.lbl Is Nothing, "", t.lbl.Text)), Nothing), 60, 10)
            addColDynamic("Nominal", If(g.Nominal IsNot Nothing, g.Nominal.Select(Function(t) If(t.lbl Is Nothing, "", t.lbl.Text)), Nothing), 60, 10)
            addColDynamic("Unit", If(g.Unit IsNot Nothing, g.Unit.Select(Function(t) If(t.lbl Is Nothing, "", t.lbl.Text)), Nothing), 60, 10)
            addColDynamic("Frequency", If(g.Frequency IsNot Nothing, g.Frequency.Select(Function(t) If(t.lbl Is Nothing, "", t.lbl.Text)), Nothing), 60, 10)
            addColDynamic("FreqUnit", If(g.FreqUnit IsNot Nothing, g.FreqUnit.Select(Function(t) If(t.lbl Is Nothing, "", t.lbl.Text)), Nothing), 60, 10)
            Exit For
        Next

        ' Fixed-width inputs/results columns
        addColFixed("MV1", 85) : addColFixed("MV2", 85) : addColFixed("MV3", 85)
        addColFixed("Average", 85) : addColFixed("Error", 85)
        addColFixed("Tolerance", 85) : addColFixed("UpperLimit", 85) : addColFixed("LowerLimit", 85)
        addColFixed("Remarks", 140) : addColFixed("Final_U", 85)

        fl.Controls.Add(hdr)
        fl.SetFlowBreak(hdr, True)

        ' ---------- Per-row placers (prefix names with SHEET NAME = SheetKey) ----------
        Dim placeLblTuple =
        Sub(ByRef tup As (Label, String), key As String, value As String, pg As ParamGroup, i As Integer, rowPanel As Panel)
            Dim sheetName As String = pg.SheetKey   ' actual worksheet/tab name
            If tup.Item1 Is Nothing Then
                tup.Item1 = New Label() With {.Name = $"{sheetName}__{key}_{i}", .AutoSize = False, .Height = 20}
            Else
                tup.Item1.Name = $"{sheetName}__{key}_{i}"
            End If
            If tup.Item1.Parent IsNot rowPanel Then tup.Item1.Parent = rowPanel
            tup.Item1.Text = value
            tup.Item1.Visible = True
            tup.Item1.Left = GetL(key)
            tup.Item1.Top = 4
            tup.Item1.Width = GetW(key)
        End Sub

        Dim placeTbTuple =
        Sub(ByRef tup As (TextBox, String), key As String, pg As ParamGroup, baseName As String, i As Integer, rowPanel As Panel)
            Dim sheetName As String = pg.SheetKey
            If tup.Item1 Is Nothing Then
                tup.Item1 = New TextBox() With {.Name = $"{sheetName}__{baseName}_{i}"}
            Else
                tup.Item1.Name = $"{sheetName}__{baseName}_{i}"
            End If
            If tup.Item1.Parent IsNot rowPanel Then tup.Item1.Parent = rowPanel
            tup.Item1.Visible = True
            tup.Item1.Width = Math.Max(0, GetW(key) - 6)
            tup.Item1.Left = GetL(key) + 3
            tup.Item1.Top = 2
        End Sub

        Dim placeLbTuple2 =
        Sub(ByRef tup As (Label, String), key As String, pg As ParamGroup, baseName As String, i As Integer, rowPanel As Panel)
            Dim sheetName As String = pg.SheetKey
            If tup.Item1 Is Nothing Then
                tup.Item1 = New Label() With {.Name = $"{sheetName}__{baseName}_{i}", .AutoSize = False, .Height = 20}
            Else
                tup.Item1.Name = $"{sheetName}__{baseName}_{i}"
            End If
            If tup.Item1.Parent IsNot rowPanel Then tup.Item1.Parent = rowPanel
            tup.Item1.Visible = True
            tup.Item1.Left = GetL(key)
            tup.Item1.Top = 4
            tup.Item1.Width = GetW(key)
        End Sub

        ' ---------- Safe getters (bounds-checked) ----------
        Dim GetLblText As Func(Of (Label, String)(), Integer, String) =
        Function(arr As (Label, String)(), idx As Integer) As String
            If arr Is Nothing OrElse idx < 0 OrElse idx >= arr.Length Then Return ""
            Dim lb = arr(idx).Item1
            Return If(lb Is Nothing, "", lb.Text)
        End Function

        ' ---------- Row builder ----------
        Dim addRow =
        Sub(g As ParamGroup, i As Integer)
            If g Is Nothing Then Exit Sub

            Dim rowPanel As New Panel() With {
                .Margin = New Padding(0, 0, 0, 6),
                .Padding = New Padding(0),
                .BorderStyle = BorderStyle.FixedSingle,
                .Width = availW,
                .Height = 26,
                .BackColor = Color.FromArgb(248, 248, 248)
            }

            Dim rngTxt As String = GetLblText(g.RangeLabel, i)
            Dim fnTxt As String = GetLblText(g.COL_FUNCTION, i)
            Dim nomTxt As String = GetLblText(g.Nominal, i)
            Dim untTxt As String = GetLblText(g.Unit, i)
            Dim frqTxt As String = GetLblText(g.Frequency, i)
            Dim fuTxt As String = GetLblText(g.FreqUnit, i)

            ' --- LEFT LABELS (never reuse last element) ---
            If g.COL_FUNCTION IsNot Nothing AndAlso i < g.COL_FUNCTION.Length Then
                placeLblTuple(g.COL_FUNCTION(i), "Function", fnTxt, g, i, rowPanel)
            Else
                Dim tmp As (Label, String) = (Nothing, Nothing)
                placeLblTuple(tmp, "Function", fnTxt, g, i, rowPanel)
            End If

            If g.RangeLabel IsNot Nothing AndAlso i < g.RangeLabel.Length Then
                placeLblTuple(g.RangeLabel(i), "RangeLabel", rngTxt, g, i, rowPanel)
            Else
                Dim tmp As (Label, String) = (Nothing, Nothing)
                placeLblTuple(tmp, "RangeLabel", rngTxt, g, i, rowPanel)
            End If

            If g.Nominal IsNot Nothing AndAlso i < g.Nominal.Length Then
                placeLblTuple(g.Nominal(i), "Nominal", nomTxt, g, i, rowPanel)
            Else
                Dim tmp As (Label, String) = (Nothing, Nothing)
                placeLblTuple(tmp, "Nominal", nomTxt, g, i, rowPanel)
            End If

            If g.Unit IsNot Nothing AndAlso i < g.Unit.Length Then
                placeLblTuple(g.Unit(i), "Unit", untTxt, g, i, rowPanel)
            Else
                Dim tmp As (Label, String) = (Nothing, Nothing)
                placeLblTuple(tmp, "Unit", untTxt, g, i, rowPanel)
            End If

            If g.Frequency IsNot Nothing AndAlso i < g.Frequency.Length Then
                placeLblTuple(g.Frequency(i), "Frequency", frqTxt, g, i, rowPanel)
            Else
                Dim tmp As (Label, String) = (Nothing, Nothing)
                placeLblTuple(tmp, "Frequency", frqTxt, g, i, rowPanel)
            End If

            If g.FreqUnit IsNot Nothing AndAlso i < g.FreqUnit.Length Then
                placeLblTuple(g.FreqUnit(i), "FreqUnit", fuTxt, g, i, rowPanel)
            Else
                Dim tmp As (Label, String) = (Nothing, Nothing)
                placeLblTuple(tmp, "FreqUnit", fuTxt, g, i, rowPanel)
            End If

            ' --- INPUTS / OUTPUTS (skip if OOB; don't fabricate TBs) ---
            If g.MV1 IsNot Nothing AndAlso i < g.MV1.Length Then placeTbTuple(g.MV1(i), "MV1", g, "MV1", i, rowPanel)
            If g.MV2 IsNot Nothing AndAlso i < g.MV2.Length Then placeTbTuple(g.MV2(i), "MV2", g, "MV2", i, rowPanel)
            If g.MV3 IsNot Nothing AndAlso i < g.MV3.Length Then placeTbTuple(g.MV3(i), "MV3", g, "MV3", i, rowPanel)

            If g.Average IsNot Nothing AndAlso i < g.Average.Length Then placeLbTuple2(g.Average(i), "Average", g, "AVG", i, rowPanel)
            If g.Error IsNot Nothing AndAlso i < g.Error.Length Then placeLbTuple2(g.Error(i), "Error", g, "ERR", i, rowPanel)
            If g.Tolerance IsNot Nothing AndAlso i < g.Tolerance.Length Then placeTbTuple(g.Tolerance(i), "Tolerance", g, "TOL", i, rowPanel)
            If g.UpperLimit IsNot Nothing AndAlso i < g.UpperLimit.Length Then placeTbTuple(g.UpperLimit(i), "UpperLimit", g, "UP", i, rowPanel)
            If g.LowerLimit IsNot Nothing AndAlso i < g.LowerLimit.Length Then placeTbTuple(g.LowerLimit(i), "LowerLimit", g, "LO", i, rowPanel)
            If g.Remarks IsNot Nothing AndAlso i < g.Remarks.Length Then placeTbTuple(g.Remarks(i), "Remarks", g, "REM", i, rowPanel)
            If g.FinalUncDecl IsNot Nothing AndAlso i < g.FinalUncDecl.Length Then placeLbTuple2(g.FinalUncDecl(i), "Final_U", g, "UNC", i, rowPanel)

            fl.Controls.Add(rowPanel)
        End Sub

        ' ---------- Build rows (original insertion order) ----------
        For Each g In ordered
            If g Is Nothing Then Continue For

            Dim headerText = $"Sheet: {g.SheetKey}"
            Dim sheetHdr As New Label() With {
            .AutoSize = False, .Height = 20, .Width = availW,
            .Text = headerText,
            .Font = New Font(Me.Font, FontStyle.Bold)
        }
            fl.Controls.Add(sheetHdr)
            fl.SetFlowBreak(sheetHdr, True)

            Dim nRows As Integer = Math.Max(1, g.TemplateRowCount)
            For i As Integer = 0 To nRows - 1
                addRow(g, i)
            Next
        Next

        fl.AutoScroll = oldScroll
        fl.ResumeLayout()
        fl.PerformLayout()
    End Sub

    Private Sub ApplyCategoriesAndSelection()
        If Groups Is Nothing OrElse Groups.Count = 0 Then Exit Sub

        ' ----- helpers -----
        Dim MakeAddr As Func(Of String, Integer, String) =
        Function(col As String, r As Integer) $"{col}{r}"

        Dim TryGetRow As Func(Of String, Integer) =
        Function(addr As String) As Integer
            If String.IsNullOrWhiteSpace(addr) Then Return -1
            Dim m = System.Text.RegularExpressions.Regex.Match(addr.Trim(), "^\s*[A-Za-z]+(\d+)\s*$")
            If Not m.Success Then Return -1
            Return Integer.Parse(m.Groups(1).Value)
        End Function

        Dim Stamp As Action(Of Control, String) =
        Sub(c As Control, addr As String)
            If c Is Nothing OrElse String.IsNullOrWhiteSpace(addr) Then Exit Sub
            c.Tag = addr : c.AccessibleName = addr
            Dim base As String = If(c.Name, "")
            Dim cut As Integer = base.IndexOf("__", StringComparison.Ordinal)
            If cut >= 0 Then base = base.Substring(0, cut)
            c.Name = base & "__" & addr
        End Sub

        Dim GetAddrLbl As Func(Of (lbl As Label, cell As String)(), Integer, String) =
        Function(arr, i)
            If arr Is Nothing OrElse i < 0 OrElse i >= arr.Length Then Return Nothing
            Return arr(i).cell
        End Function

        Dim GetAddrTb As Func(Of (tb As TextBox, cell As String)(), Integer, String) =
        Function(arr, i)
            If arr Is Nothing OrElse i < 0 OrElse i >= arr.Length Then Return Nothing
            Return arr(i).cell
        End Function

        ' Row from a specific index using any mapped cell at that index
        Dim RowFromIndex As Func(Of ParamGroup, Integer, Integer) =
        Function(g As ParamGroup, i As Integer) As Integer
            Dim candidates As New List(Of String) From {
                GetAddrLbl(g.COL_FUNCTION, i),
                GetAddrLbl(g.RangeLabel, i),
                GetAddrLbl(g.Nominal, i),
                GetAddrLbl(g.Unit, i),
                GetAddrLbl(g.Frequency, i),
                GetAddrLbl(g.FreqUnit, i),
                GetAddrTb(g.MV1, i),
                GetAddrTb(g.MV2, i),
                GetAddrTb(g.MV3, i),
                GetAddrTb(g.Tolerance, i),
                GetAddrTb(g.UpperLimit, i),
                GetAddrTb(g.LowerLimit, i),
                GetAddrTb(g.Remarks, i),
                GetAddrLbl(g.Average, i),
                GetAddrLbl(g.Error, i),
                GetAddrLbl(g.FinalUncDecl, i)
            }
            For Each a In candidates
                Dim r = TryGetRow(a)
                If r > 0 Then Return r
            Next
            Return -1
        End Function

        ' Find the first (starting) row for this group:
        ' prefer index 0’s mapped cells; otherwise scan the sheet lightly
        Dim FindStartRow As Func(Of ParamGroup, Object, Integer) =
        Function(g As ParamGroup, ws As Object) As Integer
            Dim r0 = RowFromIndex(g, 0)
            If r0 > 0 Then Return r0

            ' light scan for first non-empty data row (A..F or G..Q)
            Dim cols() As String = {"A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "N", "O", "P", "Q", "AI"}
            For r As Integer = 2 To 200
                For Each col In cols
                    Dim v = CalRowModule.ReadCell(ws, $"{col}{r}")
                    If Not String.IsNullOrWhiteSpace(v) Then Return r
                Next
            Next
            Return -1
        End Function

        For Each kv In Groups
            Dim g = kv.Value
            If g Is Nothing Then Continue For
            Dim sheetName As String = If(String.IsNullOrWhiteSpace(g.SheetKey), kv.Key, g.SheetKey)
            SetSheetsIfChanged(ctxDc, sheetName)
            CalRowModule.WithWorksheet(ctxDc,
            Sub(ws)

                ' how many rows exist in this group
                Dim n As Integer = 0
                n = Math.Max(n, If(g.COL_FUNCTION IsNot Nothing, g.COL_FUNCTION.Length, 0))
                n = Math.Max(n, If(g.RangeLabel IsNot Nothing, g.RangeLabel.Length, 0))
                n = Math.Max(n, If(g.Nominal IsNot Nothing, g.Nominal.Length, 0))
                n = Math.Max(n, If(g.Unit IsNot Nothing, g.Unit.Length, 0))
                n = Math.Max(n, If(g.Frequency IsNot Nothing, g.Frequency.Length, 0))
                n = Math.Max(n, If(g.FreqUnit IsNot Nothing, g.FreqUnit.Length, 0))
                n = Math.Max(n, If(g.MV1 IsNot Nothing, g.MV1.Length, 0))
                n = Math.Max(n, If(g.MV2 IsNot Nothing, g.MV2.Length, 0))
                n = Math.Max(n, If(g.MV3 IsNot Nothing, g.MV3.Length, 0))
                n = Math.Max(n, If(g.Average IsNot Nothing, g.Average.Length, 0))
                n = Math.Max(n, If(g.Error IsNot Nothing, g.Error.Length, 0))
                n = Math.Max(n, If(g.FinalUncDecl IsNot Nothing, g.FinalUncDecl.Length, 0))
                n = Math.Max(n, If(g.Tolerance IsNot Nothing, g.Tolerance.Length, 0))
                n = Math.Max(n, If(g.UpperLimit IsNot Nothing, g.UpperLimit.Length, 0))
                n = Math.Max(n, If(g.LowerLimit IsNot Nothing, g.LowerLimit.Length, 0))
                n = Math.Max(n, If(g.Remarks IsNot Nothing, g.Remarks.Length, 0))
                n = Math.Max(n, Math.Max(1, g.TemplateRowCount))

                ' discover the group start row once
                Dim startRow As Integer = FindStartRow(g, ws)

                For i As Integer = 0 To n - 1
                    Dim excelRow As Integer = RowFromIndex(g, i)
                    If excelRow <= 0 AndAlso startRow > 0 Then excelRow = startRow + i
                    If excelRow <= 0 Then
                        SetRowVisible(g, i, False)
                        Continue For
                    End If

                    ' ----- LEFT (A..F) -----
                    Dim a_fn = CalRowModule.ReadCell(ws, MakeAddr("A", excelRow))
                    Dim b_rng = CalRowModule.ReadCell(ws, MakeAddr("B", excelRow))
                    Dim c_nom = CalRowModule.ReadCell(ws, MakeAddr("C", excelRow))
                    Dim d_unit = CalRowModule.ReadCell(ws, MakeAddr("D", excelRow))
                    Dim e_frq = CalRowModule.ReadCell(ws, MakeAddr("E", excelRow))
                    Dim f_fu = CalRowModule.ReadCell(ws, MakeAddr("F", excelRow))

                    If g.COL_FUNCTION IsNot Nothing AndAlso i < g.COL_FUNCTION.Length Then
                        If String.IsNullOrWhiteSpace(g.COL_FUNCTION(i).cell) Then g.COL_FUNCTION(i).cell = "A" & excelRow
                        If g.COL_FUNCTION(i).lbl IsNot Nothing Then
                            g.COL_FUNCTION(i).lbl.Text = a_fn : g.COL_FUNCTION(i).lbl.Visible = True
                            Stamp(g.COL_FUNCTION(i).lbl, g.COL_FUNCTION(i).cell)
                        End If
                    End If
                    If g.RangeLabel IsNot Nothing AndAlso i < g.RangeLabel.Length Then
                        If String.IsNullOrWhiteSpace(g.RangeLabel(i).cell) Then g.RangeLabel(i).cell = "B" & excelRow
                        If g.RangeLabel(i).lbl IsNot Nothing Then
                            g.RangeLabel(i).lbl.Text = b_rng : g.RangeLabel(i).lbl.Visible = True
                            Stamp(g.RangeLabel(i).lbl, g.RangeLabel(i).cell)
                        End If
                    End If
                    If g.Nominal IsNot Nothing AndAlso i < g.Nominal.Length Then
                        If String.IsNullOrWhiteSpace(g.Nominal(i).cell) Then g.Nominal(i).cell = "C" & excelRow
                        If g.Nominal(i).lbl IsNot Nothing Then
                            g.Nominal(i).lbl.Text = c_nom : g.Nominal(i).lbl.Visible = True
                            Stamp(g.Nominal(i).lbl, g.Nominal(i).cell)
                        End If
                    End If
                    If g.Unit IsNot Nothing AndAlso i < g.Unit.Length Then
                        If String.IsNullOrWhiteSpace(g.Unit(i).cell) Then g.Unit(i).cell = "D" & excelRow
                        If g.Unit(i).lbl IsNot Nothing Then
                            g.Unit(i).lbl.Text = d_unit : g.Unit(i).lbl.Visible = True
                            Stamp(g.Unit(i).lbl, g.Unit(i).cell)
                        End If
                    End If
                    If g.Frequency IsNot Nothing AndAlso i < g.Frequency.Length Then
                        If String.IsNullOrWhiteSpace(g.Frequency(i).cell) Then g.Frequency(i).cell = "E" & excelRow
                        If g.Frequency(i).lbl IsNot Nothing Then
                            g.Frequency(i).lbl.Text = e_frq : g.Frequency(i).lbl.Visible = True
                            Stamp(g.Frequency(i).lbl, g.Frequency(i).cell)
                        End If
                    End If
                    If g.FreqUnit IsNot Nothing AndAlso i < g.FreqUnit.Length Then
                        If String.IsNullOrWhiteSpace(g.FreqUnit(i).cell) Then g.FreqUnit(i).cell = "F" & excelRow
                        If g.FreqUnit(i).lbl IsNot Nothing Then
                            g.FreqUnit(i).lbl.Text = f_fu : g.FreqUnit(i).lbl.Visible = True
                            Stamp(g.FreqUnit(i).lbl, g.FreqUnit(i).cell)
                        End If
                    End If

                    ' ----- RIGHT (computed labels) -----
                    If g.Average IsNot Nothing AndAlso i < g.Average.Length Then
                        If String.IsNullOrWhiteSpace(g.Average(i).cell) Then g.Average(i).cell = "J" & excelRow
                        If g.Average(i).lbl IsNot Nothing Then
                            g.Average(i).lbl.Text = CalRowModule.ReadCell(ws, g.Average(i).cell)
                            g.Average(i).lbl.Visible = True
                            Stamp(g.Average(i).lbl, g.Average(i).cell)
                        End If
                    End If
                    If g.[Error] IsNot Nothing AndAlso i < g.[Error].Length Then
                        If String.IsNullOrWhiteSpace(g.[Error](i).cell) Then g.[Error](i).cell = "K" & excelRow
                        If g.[Error](i).lbl IsNot Nothing Then
                            g.[Error](i).lbl.Text = CalRowModule.ReadCell(ws, g.[Error](i).cell)
                            g.[Error](i).lbl.Visible = True
                            Stamp(g.[Error](i).lbl, g.[Error](i).cell)
                        End If
                    End If
                    If g.FinalUncDecl IsNot Nothing AndAlso i < g.FinalUncDecl.Length Then
                        If String.IsNullOrWhiteSpace(g.FinalUncDecl(i).cell) Then g.FinalUncDecl(i).cell = "AI" & excelRow
                        If g.FinalUncDecl(i).lbl IsNot Nothing Then
                            g.FinalUncDecl(i).lbl.Text = CalRowModule.ReadCell(ws, g.FinalUncDecl(i).cell)
                            g.FinalUncDecl(i).lbl.Visible = True
                            Stamp(g.FinalUncDecl(i).lbl, g.FinalUncDecl(i).cell)
                        End If
                    End If
                    'DCV__MV1_0
                    ' ----- RIGHT (inputs/limits/remarks) -----
                    If g.MV1 IsNot Nothing AndAlso i < g.MV1.Length Then
                        If String.IsNullOrWhiteSpace(g.MV1(i).cell) Then g.MV1(i).cell = "G" & excelRow
                        If g.MV1(i).tb IsNot Nothing Then
                            g.MV1(i).tb.Text = CalRowModule.ReadCell(ws, g.MV1(i).cell)
                            g.MV1(i).tb.Visible = True
                            Stamp(g.MV1(i).tb, g.MV1(i).cell)
                            stringList.Add(g.MV1(i).tb.Name)
                        End If
                    End If
                    If g.MV2 IsNot Nothing AndAlso i < g.MV2.Length Then
                        If String.IsNullOrWhiteSpace(g.MV2(i).cell) Then g.MV2(i).cell = "H" & excelRow
                        If g.MV2(i).tb IsNot Nothing Then
                            g.MV2(i).tb.Text = CalRowModule.ReadCell(ws, g.MV2(i).cell)
                            g.MV2(i).tb.Visible = True
                            Stamp(g.MV2(i).tb, g.MV2(i).cell)
                        End If
                    End If
                    If g.MV3 IsNot Nothing AndAlso i < g.MV3.Length Then
                        If String.IsNullOrWhiteSpace(g.MV3(i).cell) Then g.MV3(i).cell = "I" & excelRow
                        If g.MV3(i).tb IsNot Nothing Then
                            g.MV3(i).tb.Text = CalRowModule.ReadCell(ws, g.MV3(i).cell)
                            g.MV3(i).tb.Visible = True
                            Stamp(g.MV3(i).tb, g.MV3(i).cell)
                        End If
                    End If
                    If g.Tolerance IsNot Nothing AndAlso i < g.Tolerance.Length Then
                        If String.IsNullOrWhiteSpace(g.Tolerance(i).cell) Then g.Tolerance(i).cell = "N" & excelRow
                        If g.Tolerance(i).tb IsNot Nothing Then
                            g.Tolerance(i).tb.Text = CalRowModule.ReadCell(ws, g.Tolerance(i).cell)
                            g.Tolerance(i).tb.Visible = True
                            Stamp(g.Tolerance(i).tb, g.Tolerance(i).cell)
                        End If
                    End If
                    If g.UpperLimit IsNot Nothing AndAlso i < g.UpperLimit.Length Then
                        If String.IsNullOrWhiteSpace(g.UpperLimit(i).cell) Then g.UpperLimit(i).cell = "O" & excelRow
                        If g.UpperLimit(i).tb IsNot Nothing Then
                            g.UpperLimit(i).tb.Text = CalRowModule.ReadCell(ws, g.UpperLimit(i).cell)
                            g.UpperLimit(i).tb.Visible = True
                            Stamp(g.UpperLimit(i).tb, g.UpperLimit(i).cell)
                        End If
                    End If
                    If g.LowerLimit IsNot Nothing AndAlso i < g.LowerLimit.Length Then
                        If String.IsNullOrWhiteSpace(g.LowerLimit(i).cell) Then g.LowerLimit(i).cell = "P" & excelRow
                        If g.LowerLimit(i).tb IsNot Nothing Then
                            g.LowerLimit(i).tb.Text = CalRowModule.ReadCell(ws, g.LowerLimit(i).cell)
                            g.LowerLimit(i).tb.Visible = True
                            Stamp(g.LowerLimit(i).tb, g.LowerLimit(i).cell)
                        End If
                    End If
                    If g.Remarks IsNot Nothing AndAlso i < g.Remarks.Length Then
                        If String.IsNullOrWhiteSpace(g.Remarks(i).cell) Then g.Remarks(i).cell = "Q" & excelRow
                        If g.Remarks(i).tb IsNot Nothing Then
                            g.Remarks(i).tb.Text = CalRowModule.ReadCell(ws, g.Remarks(i).cell)
                            g.Remarks(i).tb.Visible = True
                            Stamp(g.Remarks(i).tb, g.Remarks(i).cell)
                        End If
                    End If

                    SetRowVisible(g, i, True)
                Next
            End Sub)
        Next
    End Sub

    ' --- drop-in: set both sheet names only if changed ---
    Private Sub SetSheetsIfChanged(ctx As CalRowModule.RowContext, name As String)
        If ctx Is Nothing OrElse String.IsNullOrWhiteSpace(name) Then Exit Sub

        Dim sameInputs = String.Equals(ctx.SheetInputsName, name, StringComparison.OrdinalIgnoreCase)
        Dim sameFormula = String.Equals(ctx.SheetFormulaName, name, StringComparison.OrdinalIgnoreCase)
        If sameInputs AndAlso sameFormula Then Exit Sub

        ' if your context exposes a suppression flag, briefly silence events
        Dim suppressProp = ctx.GetType().GetProperty("SuppressEvents")
        Dim hadProp As Boolean = False
        If suppressProp IsNot Nothing Then
            Try
                suppressProp.SetValue(ctx, True)
                hadProp = True
            Catch
            End Try
        End If

        ctx.SheetInputsName = name
        ctx.SheetFormulaName = name

        If hadProp Then
            Try : suppressProp.SetValue(ctx, False) : Catch : End Try
        End If
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        ' 1) Save any pending data
        Try
            If ctxDc IsNot Nothing Then
                CalRowModule.SaveToExcel(ctxDc)
            End If
        Catch
            ' Swallow/Log as needed
        End Try

        ' 2) Stop video source safely
        Try
            If videoSource IsNot Nothing Then
                RemoveHandler videoSource.NewFrame, AddressOf Video_NewFrame
                If videoSource.IsRunning Then
                    videoSource.SignalToStop()
                    videoSource.WaitForStop()
                End If
            End If
        Catch
            ' Swallow/Log as needed
        End Try

        ' 3) Close serial port
        Try
            If SerialPort1 IsNot Nothing AndAlso SerialPort1.IsOpen Then
                SerialPort1.Write("bigsj")
                SerialPort1.Write("OOUT 0OHMX")
                SerialPort1.Write("OSTBYX")
                SerialPort1.Close()
            End If
        Catch
            ' Swallow/Log as needed
        End Try

        MyBase.OnFormClosing(e)
    End Sub

    ' Prefer an EXTERNAL USB webcam if present; otherwise fall back gracefully
    Private Function CreatePreferredCamera() As VideoCaptureDevice
        Dim devices = New FilterInfoCollection(FilterCategory.VideoInputDevice)
        If devices Is Nothing OrElse devices.Count = 0 Then Return Nothing

        Dim externalKeywords = New String() {
        "logi", "logitech", "brio", "c920", "c922", "c925", "c930",
        "microsoft", "lifecam", "creative", "razer", "elgato", "aver",
        "aukey", "hd pro", "usb", "webcam hd", "camera hd"
    }
        Dim internalKeywords = New String() {
        "integrated", "internal", "built-in", "builtin", "laptop",
        "hd camera", "front camera"
    }

        Dim pick As FilterInfo = Nothing

        For Each d As FilterInfo In devices
            Dim n = d.Name.ToLowerInvariant()
            If externalKeywords.Any(Function(k) n.Contains(k)) Then
                pick = d : Exit For
            End If
        Next

        If pick Is Nothing Then
            For Each d As FilterInfo In devices
                Dim n = d.Name.ToLowerInvariant()
                If Not internalKeywords.Any(Function(k) n.Contains(k)) Then
                    pick = d : Exit For
                End If
            Next
        End If

        If pick Is Nothing Then pick = devices(0)

        Dim cam = New VideoCaptureDevice(pick.MonikerString)
        Try
            Dim caps = cam.VideoCapabilities
            If caps IsNot Nothing AndAlso caps.Length > 0 Then
                Dim best = caps.FirstOrDefault(Function(c) c.FrameSize.Width = 1280 AndAlso c.FrameSize.Height = 720)
                If best Is Nothing Then
                    best = caps.OrderByDescending(Function(c) c.FrameSize.Width * c.FrameSize.Height).First()
                End If
                cam.VideoResolution = best
            End If
        Catch
        End Try

        Return cam
    End Function

    ' Read worksheet names from an .xlsx without Excel Interop
    Private Function GetWorksheetNamesFromXlsx(path As String) As List(Of String)
        Dim names As New List(Of String)
        Try
            If String.IsNullOrWhiteSpace(path) OrElse Not IO.File.Exists(path) Then Return names
            Using pkg = Package.Open(path, FileMode.Open, FileAccess.Read)
                Dim partUri = New Uri("/xl/workbook.xml", UriKind.Relative)
                Dim part = pkg.GetPart(partUri)
                Using s = part.GetStream(FileMode.Open, FileAccess.Read)
                    Dim doc = System.Xml.Linq.XDocument.Load(s)
                    Dim ns = System.Xml.Linq.XNamespace.Get("http://schemas.openxmlformats.org/spreadsheetml/2006/main")
                    Dim sheets = doc.Root.Element(ns + "sheets")
                    If sheets Is Nothing Then Return names
                    For Each sh In sheets.Elements(ns + "sheet")
                        Dim n = CStr(sh.Attribute("name"))
                        If Not String.IsNullOrWhiteSpace(n) Then names.Add(n)
                    Next
                End Using
            End Using
        Catch
        End Try
        Return names
    End Function

#End Region

#Region "Portable Job_Export helpers"

    ' ========= EXPORT INTO YOUR TEMPLATE (rows with formulas + MV inline) =========
    Private Sub SimpleExportExcel()
        Dim calcPath As String = If(ctxDc IsNot Nothing, ctxDc.TemplatePath, Nothing) ' CALC workbook used during load
        Const exportTemplatePath As String = "C:\Users\dbneri\Documents\Visual Studio 2010\Projects\ASCal\ASCal\bin\Debug\exporttemplate.xlsx"

        If String.IsNullOrWhiteSpace(calcPath) OrElse Not IO.File.Exists(calcPath) Then
            MessageBox.Show("Live CALC workbook not found (ctxDc.TemplatePath).", "Export", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Exit Sub
        End If
        If Not IO.File.Exists(exportTemplatePath) Then
            MessageBox.Show("Export template not found:" & Environment.NewLine & exportTemplatePath, "Export", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Exit Sub
        End If

        Dim outDir As String = GetJobExportDir()
        If Not IO.Directory.Exists(outDir) Then IO.Directory.CreateDirectory(outDir)

        Dim base As String = IO.Path.GetFileNameWithoutExtension(BuildReportFileName())
        If String.IsNullOrWhiteSpace(base) Then base = "CalReport"
        Dim outPath As String = IO.Path.Combine(outDir, base & "" & DateTime.Now.ToString("yyyyMMdd_HHmmssfff") & "" & Guid.NewGuid().ToString("N") & ".xlsx")

        Try
            IO.File.Copy(exportTemplatePath, outPath, True)
            WriteMvTableIntoTemplate(calcPath, outPath, "Export")

            ' ensure all Package/streams are closed before opening (prevents DisconnectedContext MDA)
            GC.Collect()
            GC.WaitForPendingFinalizers()

            MessageBox.Show("Exported: " & outPath, "Export", MessageBoxButtons.OK, MessageBoxIcon.Information)

            ' open with default handler via shell (no Excel COM automation in this process)
            Try
                Dim psi As New ProcessStartInfo(outPath) With {.UseShellExecute = True}
                Process.Start(psi)
            Catch
                ' ignore if no handler
            End Try
        Catch ex As Exception
            MessageBox.Show("Export failed: " & ex.Message, "Export", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Application.Exit()
        End Try
    End Sub

    ' ===== Read CALC workbook (values + formulas), then write rows with MV inline (G/H/I) =====
    Private Sub WriteMvTableIntoTemplate(calcPath As String, outPath As String, exportSheetName As String)
        Dim ns As XNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"
        Dim rns As XNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"

        ' ---------- 1) Write headers using existing COM-based method ----------
        Dim xlApp As Object = Nothing
        Dim xlWb As Object = Nothing
        Dim xlWs As Object = Nothing
        Try
            xlApp = CreateObject("Excel.Application")
            xlApp.DisplayAlerts = False
            ' best-effort perf toggles (guard them to avoid your error)
            Try : xlApp.ScreenUpdating = False : Catch : End Try
            Try : xlApp.EnableEvents = False : Catch : End Try
            ' Some installs refuse setting Calculation; do NOT fail the export.
            Try : xlApp.Calculation = -4135  ' xlCalculationManual
            Catch : End Try

            xlWb = xlApp.Workbooks.Open(outPath, [ReadOnly]:=False)

            For Each sht As Object In xlWb.Worksheets
                If String.Equals(CStr(sht.Name), exportSheetName, StringComparison.OrdinalIgnoreCase) Then
                    xlWs = sht : Exit For
                End If
            Next
            If xlWs Is Nothing Then Throw New InvalidOperationException("Sheet '" & exportSheetName & "' not found in workbook (COM).")

            WriteAllHeaderInputsToExcel_Cells(xlWs) ' your existing mapping method
            xlWb.Save()
        Finally
            ' -------- restore & cleanup (best effort) --------
            ' Close workbook first (no save)
            If xlWb IsNot Nothing Then
                Try
                    xlWb.Close(False)
                Catch
                End Try
            End If

            ' Restore app state before quitting
            If xlApp IsNot Nothing Then
                Try
                    xlApp.Calculation = -4105   ' xlCalculationAutomatic
                Catch
                End Try
                Try
                    xlApp.EnableEvents = True
                Catch
                End Try
                Try
                    xlApp.ScreenUpdating = True
                Catch
                End Try
                Try
                    xlApp.Quit()
                Catch
                End Try
            End If

            ' Release COM references (each guarded)
            Try
                If xlWs IsNot Nothing Then System.Runtime.InteropServices.Marshal.FinalReleaseComObject(xlWs)
            Catch
            End Try
            Try
                If xlWb IsNot Nothing Then System.Runtime.InteropServices.Marshal.FinalReleaseComObject(xlWb)
            Catch
            End Try
            Try
                If xlApp IsNot Nothing Then System.Runtime.InteropServices.Marshal.FinalReleaseComObject(xlApp)
            Catch
            End Try

            xlWs = Nothing : xlWb = Nothing : xlApp = Nothing
        End Try


        ' ---------- 2) Your existing OpenXML code (unchanged helpers below) ----------
        Dim ColLetters As Func(Of String, String) =
        Function(a As String)
            Dim i = 0 : While i < a.Length AndAlso Char.IsLetter(a(i)) : i += 1 : End While
            If i = 0 Then Return "A" Else Return a.Substring(0, i)
        End Function

        Dim LoadShared As Func(Of System.IO.Packaging.Package, List(Of String)) =
        Function(p)
            Dim out As New List(Of String)
            Dim sstUri As New Uri("/xl/sharedStrings.xml", UriKind.Relative)
            If Not p.PartExists(sstUri) Then Return out
            Dim sst = XDocument.Load(p.GetPart(sstUri).GetStream(FileMode.Open, FileAccess.Read))
            For Each si In sst.Root.Elements(ns + "si")
                Dim t = si.Element(ns + "t")
                If t IsNot Nothing Then
                    out.Add(t.Value)
                Else
                    Dim sb As New Text.StringBuilder()
                    For Each run In si.Elements(ns + "r")
                        Dim rt = run.Element(ns + "t")
                        If rt IsNot Nothing Then sb.Append(rt.Value)
                    Next
                    out.Add(sb.ToString())
                End If
            Next
            Return out
        End Function

        Dim GetValue As Func(Of XElement, System.IO.Packaging.Package, List(Of String), String) =
        Function(c As XElement, pkg As System.IO.Packaging.Package, sst As List(Of String))
            Dim tt As String = CStr(c.Attribute("t"))
            If tt = "inlineStr" Then
                Dim isEl = c.Element(ns + "is") : If isEl Is Nothing Then Return ""
                Dim tnode = isEl.Element(ns + "t") : If tnode IsNot Nothing Then Return tnode.Value
                Dim sb As New Text.StringBuilder()
                For Each run In isEl.Elements(ns + "r")
                    Dim rt = run.Element(ns + "t")
                    If rt IsNot Nothing Then sb.Append(rt.Value)
                Next
                Return sb.ToString()
            ElseIf tt = "s" Then
                Dim vEl = c.Element(ns + "v") : If vEl Is Nothing Then Return ""
                Dim idx As Integer
                If Integer.TryParse(vEl.Value, idx) AndAlso idx >= 0 AndAlso idx < sst.Count Then Return sst(idx)
                Return vEl.Value
            Else
                Dim vEl = c.Element(ns + "v") : If vEl Is Nothing Then Return ""
                Return vEl.Value
            End If
        End Function

        Dim SetInline As Action(Of XElement, String, Integer?) =
        Sub(cellEl As XElement, val As String, styleIdx As Integer?)
            If val Is Nothing Then val = ""
            cellEl.SetAttributeValue("t", "inlineStr")
            cellEl.Elements().Remove()
            cellEl.Add(New XElement(ns + "is",
                New XElement(ns + "t", New XAttribute(XNamespace.Xml + "space", "preserve"), val)))
            If styleIdx.HasValue Then cellEl.SetAttributeValue("s", styleIdx.Value)
        End Sub

        Dim SetNumberOrInline As Action(Of XElement, String, Integer?) =
        Sub(cellEl As XElement, val As String, styleIdx As Integer?)
            If val Is Nothing Then val = ""
            Dim d As Decimal
            If Decimal.TryParse(val, Globalization.NumberStyles.Any, Globalization.CultureInfo.InvariantCulture, d) Then
                Dim tAttr = cellEl.Attribute("t") : If tAttr IsNot Nothing Then tAttr.Remove()
                cellEl.Elements().Remove()
                cellEl.Add(New XElement(ns + "v", d.ToString(Globalization.CultureInfo.InvariantCulture)))
                If styleIdx.HasValue Then cellEl.SetAttributeValue("s", styleIdx.Value)
            Else
                SetInline(cellEl, val, styleIdx)
            End If
        End Sub

        Dim SetFormula As Action(Of XElement, String, Integer?) =
        Sub(cellEl As XElement, f As String, styleIdx As Integer?)
            If String.IsNullOrWhiteSpace(f) Then Exit Sub
            Dim fx = If(f.StartsWith("="), f.Substring(1), f)
            Dim tAttr = cellEl.Attribute("t") : If tAttr IsNot Nothing Then tAttr.Remove()
            cellEl.Elements().Remove()
            cellEl.Add(New XElement(ns + "f", fx))
            If styleIdx.HasValue Then cellEl.SetAttributeValue("s", styleIdx.Value)
        End Sub

        Dim EnsureCell As Func(Of XElement, String, XElement) =
        Function(rowEl As XElement, addr As String) As XElement
            Dim c = rowEl.Elements(ns + "c").FirstOrDefault(Function(x) String.Equals(CStr(x.Attribute("r")), addr, StringComparison.Ordinal))
            If c Is Nothing Then
                c = New XElement(ns + "c", New XAttribute("r", addr))
                rowEl.Add(c)
            End If
            Return c
        End Function

        Dim AdjustA1FormulaWithSheetMaps As Func(Of String, String, Dictionary(Of String, Dictionary(Of Integer, Integer)), Dictionary(Of String, Integer), Integer, String) =
        Function(fx As String, currentSheet As String,
                 maps As Dictionary(Of String, Dictionary(Of Integer, Integer)),
                 fallbackDeltaBySheet As Dictionary(Of String, Integer),
                 globalFallbackDelta As Integer) As String
            If String.IsNullOrWhiteSpace(fx) Then Return fx
            Dim core As String = If(fx.StartsWith("="), fx.Substring(1), fx)
            Dim rx As New Global.System.Text.RegularExpressions.Regex(
                "(?<sheet>'[^']+'!|[A-Za-z0-9_\.]+!)?(?<col>\$?[A-Za-z]{1,3})(?<rowanchor>\$?)(?<row>\d+)",
                Global.System.Text.RegularExpressions.RegexOptions.Compiled)
            Dim result As String = rx.Replace(core, Function(m As Text.RegularExpressions.Match)
                                                        Dim rowAnchor As String = m.Groups("rowanchor").Value
                                                        If rowAnchor = "$" Then Return m.Value
                                                        Dim sheetToken As String = m.Groups("sheet").Value
                                                        Dim refSheet As String = currentSheet
                                                        If sheetToken.Length > 0 Then
                                                            Dim s = sheetToken.Substring(0, sheetToken.Length - 1)
                                                            If s.StartsWith("'") AndAlso s.EndsWith("'") AndAlso s.Length >= 2 Then
                                                                refSheet = s.Substring(1, s.Length - 2)
                                                            Else
                                                                refSheet = s
                                                            End If
                                                        End If
                                                        Dim map As Dictionary(Of Integer, Integer) = Nothing
                                                        maps.TryGetValue(refSheet, map)
                                                        Dim oldRow As Integer = Integer.Parse(m.Groups("row").Value)
                                                        Dim newRow As Integer
                                                        If map IsNot Nothing AndAlso map.TryGetValue(oldRow, newRow) Then
                                                            Return (If(sheetToken, "")) & m.Groups("col").Value & newRow.ToString()
                                                        Else
                                                            Dim delta As Integer = globalFallbackDelta
                                                            Dim dTmp As Integer
                                                            If fallbackDeltaBySheet IsNot Nothing AndAlso fallbackDeltaBySheet.TryGetValue(refSheet, dTmp) Then delta = dTmp
                                                            Dim shifted As Integer = Math.Max(1, oldRow + delta)
                                                            Return (If(sheetToken, "")) & m.Groups("col").Value & shifted.ToString()
                                                        End If
                                                    End Function)
            Return result
        End Function

        Dim colsArr As String() = {"A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z", "AA", "AB", "AC", "AD", "AE", "AF", "AG", "AH", "AI"}
        Dim colsSet As New HashSet(Of String)(colsArr, StringComparer.OrdinalIgnoreCase)

        Dim mvTriples As New Queue(Of (String, String, String))()
        If Groups IsNot Nothing AndAlso Groups.Count > 0 Then
            For Each g In Groups.Values
                If g Is Nothing Then Continue For
                Dim l1 = If(g.MV1 IsNot Nothing, g.MV1.Length, 0)
                Dim l2 = If(g.MV2 IsNot Nothing, g.MV2.Length, 0)
                Dim l3 = If(g.MV3 IsNot Nothing, g.MV3.Length, 0)
                Dim n = Math.Max(l1, Math.Max(l2, l3))
                For i = 0 To n - 1
                    Dim v1 = If(g.MV1 IsNot Nothing AndAlso i < l1 AndAlso g.MV1(i).tb IsNot Nothing, g.MV1(i).tb.Text, "")
                    Dim v2 = If(g.MV2 IsNot Nothing AndAlso i < l2 AndAlso g.MV2(i).tb IsNot Nothing, g.MV2(i).tb.Text, "")
                    Dim v3 = If(g.MV3 IsNot Nothing AndAlso i < l3 AndAlso g.MV3(i).tb IsNot Nothing, g.MV3(i).tb.Text, "")
                    mvTriples.Enqueue((v1, v2, v3))
                Next
            Next
        End If

        Dim expDoc As XDocument = Nothing
        Dim expSheetData As XElement = Nothing
        Dim expWsPart As System.IO.Packaging.PackagePart = Nothing
        Dim writeRow As Integer
        Dim numericStyleIndex As Integer? = Nothing

        Dim EnsureNumericStyle As Func(Of System.IO.Packaging.Package, Integer?) =
        Function(pkg As System.IO.Packaging.Package) As Integer?
            Dim stylesUri = New Uri("/xl/styles.xml", UriKind.Relative)
            If Not pkg.PartExists(stylesUri) Then Return Nothing
            Dim stylesPart = pkg.GetPart(stylesUri)
            Dim stylesDoc As XDocument
            Using s = stylesPart.GetStream(FileMode.Open, FileAccess.Read)
                stylesDoc = XDocument.Load(s)
            End Using
            Dim xfsParent = stylesDoc.Root.Element(ns + "cellXfs")
            If xfsParent Is Nothing Then Return Nothing
            Dim idx As Integer = 0
            For Each xf In xfsParent.Elements(ns + "xf")
                Dim numFmtIdAttr = xf.Attribute("numFmtId")
                Dim applyAttr = xf.Attribute("applyNumberFormat")
                Dim numFmtId As Integer = If(numFmtIdAttr Is Nothing, -1, Integer.Parse(numFmtIdAttr.Value))
                Dim apply As Integer = If(applyAttr Is Nothing, 0, Integer.Parse(applyAttr.Value))
                If numFmtId = 4 AndAlso apply = 1 Then Return idx
                idx += 1
            Next
            Dim newIdx As Integer = idx
            xfsParent.Add(New XElement(ns + "xf",
                New XAttribute("numFmtId", "4"),
                New XAttribute("applyNumberFormat", "1"),
                New XAttribute("fontId", "0"),
                New XAttribute("fillId", "0"),
                New XAttribute("borderId", "0"),
                New XAttribute("xfId", "0")))
            xfsParent.SetAttributeValue("count", (newIdx + 1).ToString())
            Using sW = stylesPart.GetStream(FileMode.Create, FileAccess.Write)
                stylesDoc.Save(sW)
            End Using
            Return newIdx
        End Function

        Using pExp = System.IO.Packaging.Package.Open(outPath, FileMode.Open, FileAccess.ReadWrite)
            numericStyleIndex = EnsureNumericStyle(pExp)

            Dim wbDoc = XDocument.Load(pExp.GetPart(System.IO.Packaging.PackUriHelper.CreatePartUri(New Uri("/xl/workbook.xml", UriKind.Relative))).GetStream(FileMode.Open, FileAccess.Read))
            Dim rels = XDocument.Load(pExp.GetPart(System.IO.Packaging.PackUriHelper.CreatePartUri(New Uri("/xl/_rels/workbook.xml.rels", UriKind.Relative))).GetStream(FileMode.Open, FileAccess.Read))

            Dim rid = (From s In wbDoc.Root.Element(ns + "sheets").Elements(ns + "sheet")
                       Where String.Equals(CStr(s.Attribute("name")), exportSheetName, StringComparison.OrdinalIgnoreCase)
                       Select CStr(s.Attribute(rns + "id"))).FirstOrDefault()
            If String.IsNullOrEmpty(rid) Then Throw New InvalidOperationException("Sheet '" & exportSheetName & "' not found in export template.")

            Dim target = (From r In rels.Root.Elements() Where CStr(r.Attribute("Id")) = rid Select CStr(r.Attribute("Target"))).First()
            Dim wsUri = System.IO.Packaging.PackUriHelper.CreatePartUri(New Uri("/xl/" & target.Replace("\", "/"), UriKind.Relative))
            expWsPart = pExp.GetPart(wsUri)

            Using s = expWsPart.GetStream(FileMode.Open, FileAccess.Read)
                expDoc = XDocument.Load(s)
            End Using
            expSheetData = expDoc.Root.Element(ns + "sheetData")
            If expSheetData Is Nothing Then Throw New InvalidOperationException("Export sheet has no sheetData.")

            Dim startRow As Integer = 36
            Dim maxRow As Integer = startRow - 1
            For Each rEl In expSheetData.Elements(ns + "row")
                Dim ra = rEl.Attribute("r") : If ra Is Nothing Then Continue For
                Dim n As Integer : If Integer.TryParse(ra.Value, n) AndAlso n > maxRow Then maxRow = n
            Next
            writeRow = Math.Max(startRow, maxRow + 1)

            Dim rowMapBySheet As New Dictionary(Of String, Dictionary(Of Integer, Integer))(StringComparer.OrdinalIgnoreCase)
            Dim fallbackDeltaBySheet As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

            Using pCalc = System.IO.Packaging.Package.Open(calcPath, FileMode.Open, FileAccess.Read)
                Dim sst = LoadShared(pCalc)
                Dim srcWb = XDocument.Load(pCalc.GetPart(New Uri("/xl/workbook.xml", UriKind.Relative)).GetStream(FileMode.Open, FileAccess.Read))
                Dim srcRels = XDocument.Load(pCalc.GetPart(New Uri("/xl/_rels/workbook.xml.rels", UriKind.Relative)).GetStream(FileMode.Open, FileAccess.Read))

                ' ---- PERF: only process "Portable" if it exists; else fall back to all sheets ----
                Dim portableExists As Boolean =
                srcWb.Root.Element(ns + "sheets").Elements(ns + "sheet").
                    Any(Function(s) String.Equals(CStr(s.Attribute("name")), "Portable", StringComparison.OrdinalIgnoreCase))

                For Each sh In srcWb.Root.Element(ns + "sheets").Elements(ns + "sheet")
                    Dim srcSheetName As String = CStr(sh.Attribute("name"))
                    If portableExists AndAlso Not String.Equals(srcSheetName, "Portable", StringComparison.OrdinalIgnoreCase) Then
                        Continue For ' skip non-Portable sheets for speed (safe)
                    End If

                    Dim rid2 = CStr(sh.Attribute(rns + "id"))
                    If String.IsNullOrEmpty(rid2) Then Continue For
                    Dim target2 = (From r In srcRels.Root.Elements() Where CStr(r.Attribute("Id")) = rid2 Select CStr(r.Attribute("Target"))).FirstOrDefault()
                    If String.IsNullOrEmpty(target2) Then Continue For

                    Dim wsUri2 = New Uri("/xl/" & target2.Replace("\", "/"), UriKind.Relative)
                    If Not pCalc.PartExists(wsUri2) Then Continue For

                    Dim ws = XDocument.Load(pCalc.GetPart(wsUri2).GetStream(FileMode.Open, FileAccess.Read))
                    Dim sd = ws.Root.Element(ns + "sheetData")
                    If sd Is Nothing Then Continue For

                    ' PRESCAN: shared formula masters (store base row)
                    Dim sharedFx As New Dictionary(Of String, (fx As String, baseRow As Integer))(StringComparer.Ordinal)
                    For Each rr In sd.Elements(ns + "row")
                        Dim rrn As Integer = 0 : Integer.TryParse(CStr(rr.Attribute("r")), rrn)
                        For Each cc In rr.Elements(ns + "c")
                            Dim fEl = cc.Element(ns + "f")
                            If fEl Is Nothing Then Continue For
                            If String.Equals(CStr(fEl.Attribute("t")), "shared", StringComparison.OrdinalIgnoreCase) Then
                                Dim si As String = CStr(fEl.Attribute("si"))
                                Dim fxText As String = fEl.Value
                                If Not String.IsNullOrWhiteSpace(si) AndAlso Not String.IsNullOrWhiteSpace(fxText) Then
                                    If Not sharedFx.ContainsKey(si) Then sharedFx(si) = (fxText, rrn)
                                End If
                            End If
                        Next
                    Next

                    If Not rowMapBySheet.ContainsKey(srcSheetName) Then rowMapBySheet(srcSheetName) = New Dictionary(Of Integer, Integer)()
                    Dim styleIdx As Integer? = numericStyleIndex
                    Dim firstDeltaSet As Boolean = False

                    For Each srcRow In sd.Elements(ns + "row")
                        Dim ra = srcRow.Attribute("r")
                        Dim srcRowNum As Integer
                        If ra Is Nothing OrElse Not Integer.TryParse(ra.Value, srcRowNum) OrElse srcRowNum < 2 Then Continue For

                        Dim bucket As New Dictionary(Of String, (hasF As Boolean, txt As String))(StringComparer.OrdinalIgnoreCase)
                        Dim anyCell As Boolean = False

                        For Each c In srcRow.Elements(ns + "c")
                            Dim addr As String = CStr(c.Attribute("r")) : If String.IsNullOrEmpty(addr) Then Continue For
                            Dim col As String = ColLetters(addr).ToUpperInvariant()

                            If Not anyCell Then
                                If c.Element(ns + "v") IsNot Nothing OrElse c.Element(ns + "f") IsNot Nothing OrElse c.Element(ns + "is") IsNot Nothing Then
                                    anyCell = True
                                End If
                            End If

                            If Not colsSet.Contains(col) Then Continue For

                            Dim fEl = c.Element(ns + "f")
                            If fEl IsNot Nothing Then
                                Dim tAttr As String = CStr(fEl.Attribute("t"))
                                If String.Equals(tAttr, "shared", StringComparison.OrdinalIgnoreCase) Then
                                    Dim si As String = CStr(fEl.Attribute("si"))
                                    Dim resolved As String = Nothing
                                    Dim baseRow As Integer = srcRowNum
                                    If Not String.IsNullOrWhiteSpace(fEl.Value) Then
                                        resolved = fEl.Value : baseRow = srcRowNum
                                    ElseIf Not String.IsNullOrWhiteSpace(si) Then
                                        Dim info As (fx As String, baseRow As Integer)
                                        If sharedFx.TryGetValue(si, info) Then
                                            resolved = info.fx : baseRow = info.baseRow
                                        End If
                                    End If
                                    If Not String.IsNullOrWhiteSpace(resolved) Then
                                        Dim rowShift As Integer = srcRowNum - baseRow
                                        Dim followerFx As String = If(rowShift <> 0, AdjustA1Formula(resolved, rowShift), resolved)
                                        bucket(col) = (True, followerFx) : anyCell = True
                                        Continue For
                                    End If
                                ElseIf Not String.IsNullOrWhiteSpace(fEl.Value) Then
                                    bucket(col) = (True, fEl.Value) : anyCell = True
                                    Continue For
                                End If
                            End If

                            Dim val As String = GetValue(c, pCalc, sst)
                            If val IsNot Nothing AndAlso val.Length > 0 Then
                                bucket(col) = (False, val) : anyCell = True
                            End If
                        Next

                        If Not anyCell Then Continue For

                        Dim rowEl As New XElement(ns + "row", New XAttribute("r", writeRow))
                        expSheetData.Add(rowEl)

                        Dim sheetMap = rowMapBySheet(srcSheetName)
                        If Not sheetMap.ContainsKey(srcRowNum) Then sheetMap(srcRowNum) = writeRow

                        If Not firstDeltaSet Then
                            fallbackDeltaBySheet(srcSheetName) = writeRow - srcRowNum
                            firstDeltaSet = True
                        End If
                        Dim globalFallbackDelta As Integer = writeRow - srcRowNum

                        For Each col In New String() {"A", "B", "C", "D", "E", "F"}
                            If bucket.ContainsKey(col) Then
                                Dim cell = EnsureCell(rowEl, col & writeRow.ToString())
                                Dim payload = bucket(col)
                                If payload.hasF Then
                                    Dim adj = AdjustA1FormulaWithSheetMaps(payload.txt, srcSheetName, rowMapBySheet, fallbackDeltaBySheet, globalFallbackDelta)
                                    SetFormula(cell, adj, styleIdx)
                                Else
                                    SetNumberOrInline(cell, payload.txt, styleIdx)
                                End If
                            End If
                        Next

                        If mvTriples.Count > 0 Then
                            Dim t = mvTriples.Dequeue()
                            If t.Item1 <> "" Then SetNumberOrInline(EnsureCell(rowEl, "G" & writeRow.ToString()), t.Item1, styleIdx)
                            If t.Item2 <> "" Then SetNumberOrInline(EnsureCell(rowEl, "H" & writeRow.ToString()), t.Item2, styleIdx)
                            If t.Item3 <> "" Then SetNumberOrInline(EnsureCell(rowEl, "I" & writeRow.ToString()), t.Item3, styleIdx)
                        End If

                        For Each col In New String() {"J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z", "AA", "AB", "AC", "AD", "AE", "AF", "AG", "AH", "AI"}
                            If bucket.ContainsKey(col) Then
                                Dim cell = EnsureCell(rowEl, col & writeRow.ToString())
                                Dim payload = bucket(col)
                                If payload.hasF Then
                                    Dim adj = AdjustA1FormulaWithSheetMaps(payload.txt, srcSheetName, rowMapBySheet, fallbackDeltaBySheet, globalFallbackDelta)
                                    SetFormula(cell, adj, styleIdx)
                                Else
                                    SetNumberOrInline(cell, payload.txt, styleIdx)
                                End If
                            End If
                        Next

                        writeRow += 1
                    Next
                Next
            End Using

            ' save only the worksheet (compact, fast)
            Dim wset As New XmlWriterSettings With {.Encoding = New Text.UTF8Encoding(False), .Indent = False, .OmitXmlDeclaration = False}
            Using sW = expWsPart.GetStream(FileMode.Create, FileAccess.Write)
                Using xw = XmlWriter.Create(sW, wset)
                    expDoc.Save(xw)
                End Using
            End Using
        End Using
    End Sub

    ' Returns a portable Job_Export folder.
    Private Function GetJobExportDir() As String
        Dim dir1 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Job_Export")
        Try
            Directory.CreateDirectory(dir1)
            Return dir1
        Catch ex As UnauthorizedAccessException
            Dim dir2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ASCal", "Job_Export")
            Directory.CreateDirectory(dir2)
            Return dir2
        End Try
    End Function

    Private Function BuildReportFileName() As String
        Return $"CalReport_{NormalizeFile(WorkOrderNumber)}_{NormalizeFile(SerialNumber)}.xlsx"
    End Function

    Private Function AdjustA1Formula(ByVal fx As String, ByVal deltaRow As Integer) As String
        If String.IsNullOrWhiteSpace(fx) OrElse deltaRow = 0 Then Return fx
        Dim core As String = If(fx.StartsWith("="), fx.Substring(1), fx)
        Dim rx As New System.Text.RegularExpressions.Regex(
        "(?<sheet>'[^']+'!|[A-Za-z0-9_\.]+!)?(?<col>\$?[A-Za-z]{1,3})(?<rowanchor>\$?)(?<row>\d+)",
        System.Text.RegularExpressions.RegexOptions.Compiled)
        Dim result As String = rx.Replace(core, Function(m As System.Text.RegularExpressions.Match)
                                                    Dim rowAnchor As String = m.Groups("rowanchor").Value
                                                    If rowAnchor = "$" Then Return m.Value
                                                    Dim rowNum As Integer = Integer.Parse(m.Groups("row").Value)
                                                    Dim shifted As Integer = Math.Max(1, rowNum + deltaRow)
                                                    Dim sheetPart As String = m.Groups("sheet").Value
                                                    Dim colPart As String = m.Groups("col").Value
                                                    Return sheetPart & colPart & shifted.ToString()
                                                End Function)
        Return result
    End Function
#End Region



#Region "Header write (cells mapping)" 'inputs na galing sa calibrate.vb

    Private Sub WriteAllHeaderInputsToExcel_Cells(ws As Object)
        ' --- Header / Identification (left) ---
        WriteIfNotEmpty(ws, "L3", WorkOrderNumber)      ' Work Order Number
        WriteIfNotEmpty(ws, "B5", Description)          ' Description
        WriteIfNotEmpty(ws, "B6", Manufacturer)         ' Manufacturer
        WriteIfNotEmpty(ws, "B7", Model)               ' Model
        WriteIfNotEmpty(ws, "B8", SerialNumber)        ' Serial Number
        WriteIfNotEmpty(ws, "B9", Range)               ' Range
        WriteIfNotEmpty(ws, "B10", Readability)        ' Res/Readability
        WriteIfNotEmpty(ws, "L11", PrevSesCalCert)     ' Prev. SES Cal Cert

        ' --- Header / Identification (right) ---
        WriteIfNotEmpty(ws, "L5", ReceivedDate)        ' Received date (moved off L3 to avoid conflict)
        WriteIfNotEmpty(ws, "L6", CalibrationDate)     ' Calibration date
        WriteIfNotEmpty(ws, "L7", OptionsInstalled)    ' Options installed
        WriteIfNotEmpty(ws, "L8", CustomerPO)          ' Customer PO
        WriteIfNotEmpty(ws, "L9", AssetNumber)         ' Asset number
        WriteIfNotEmpty(ws, "L10", AccuracyHeader)     ' Accuracy header
        WriteIfNotEmpty(ws, "L11", PreviousTechnician) ' Previous technician

        ' --- Company ---
        WriteIfNotEmpty(ws, "B14", CompanyName)        ' Company name
        WriteIfNotEmpty(ws, "B15", CompanyAddress)     ' Company address

        ' --- In-house / On-site flags & address (kept from previous; update cells if template changed) ---
        Dim ct = If(CalibrationType, "").Trim().ToUpperInvariant()
        If ct.Contains("IN-HOUSE") OrElse ct.Contains("INHOUSE") Then
            WriteIfNotEmpty(ws, "J14", "x")           ' in-house checked
        ElseIf ct.Contains("ON-SITE") OrElse ct.Contains("ONSITE") Then
            WriteIfNotEmpty(ws, "J15", "x")           ' on-site checked
            WriteIfNotEmpty(ws, "J16", SpecificSite)  ' on-site address
        End If

        ' --- Reference Standards (rows 19–20) ---
        WriteIfNotEmpty(ws, "A19", RefDesc1)
        WriteIfNotEmpty(ws, "D19", RefSN1)
        WriteIfNotEmpty(ws, "G19", RefCalRef1)
        WriteIfNotEmpty(ws, "J19", RefDue1)

        WriteIfNotEmpty(ws, "A20", RefDesc2)
        WriteIfNotEmpty(ws, "D20", RefSN2)
        WriteIfNotEmpty(ws, "G20", RefCalRef2)
        WriteIfNotEmpty(ws, "J20", RefDue2)

        ' --- Accessories (rows 23–24) ---
        WriteIfNotEmpty(ws, "A23", AccDesc1)
        WriteIfNotEmpty(ws, "D23", AccSN1)
        WriteIfNotEmpty(ws, "G23", AccCalBrand1)
        WriteIfNotEmpty(ws, "J23", AccModel1)

        WriteIfNotEmpty(ws, "A24", AccDesc2)
        WriteIfNotEmpty(ws, "D24", AccSN2)
        WriteIfNotEmpty(ws, "G24", AccCalBrand2)
        WriteIfNotEmpty(ws, "J24", AccModel2)

        ' --- Environmental conditions ---
        WriteIfNotEmpty(ws, "B27", TempStart)          ' Temperature Start
        WriteIfNotEmpty(ws, "B28", TempEnd)            ' Temperature End
        WriteIfNotEmpty(ws, "E27", HumidityStart)      ' Relative Humidity Start
        WriteIfNotEmpty(ws, "E28", HumidityEnd)        ' Relative Humidity End

        ' --- Method ---
        WriteIfNotEmpty(ws, "J26", calMathod)          ' Calibration Method
    End Sub

    Private Sub WriteIfNotEmpty(ws As Object, addr As String, value As String)
        If String.IsNullOrWhiteSpace(value) Then Exit Sub
        WriteCell(ws, addr, value)
    End Sub

#End Region


#Region "Live compute plumbing" 'mag-aactivate eto kapag nagmanual input sa lahat ng fields

    ' === MV textbox change event ===
    ' Recalc when MV changes
    Private Sub OnMvChanged(sender As Object, e As EventArgs)
        Dim tb = TryCast(sender, TextBox)
        Dim g As ParamGroup = Nothing
        Dim i As Integer = -1
        If Not ResolveOwner(tb, g, i) Then Exit Sub

        currentGroup = g
        currentRowIdx = i
        Dim excelRow As Integer = GetRowFromAddr(g.MV3(i).Item2) ' cell addr is Item2
        currentExcelRow = excelRow
        ctxDc.TargetRow = excelRow

        ctxDc.PreCalculate = Sub(ws) WriteInputsRow(ws, g, i)
        ctxDc.AfterCalculate = Sub(ws) ReadOutputsRow(ws, g, i)
        CalRowModule.RecalculateNow(ctxDc)
    End Sub

    ' === HookLiveCompute (Sub) ===
    ' Attach TextChanged to MV boxes
    ' Attach lightweight handlers: MV1/MV2 do NOT compute; MV3 computes when row is complete
    Private Sub HookLiveCompute()
        Dim attach = Sub(arr As (TextBox, String)(), isMv3 As Boolean)
                         If arr Is Nothing Then Exit Sub
                         For Each p In arr
                             If p.Item1 Is Nothing Then Continue For

                             ' Clean out any previous handlers (including the old OnMvChanged)
                             RemoveHandler p.Item1.TextChanged, AddressOf OnMvChanged
                             RemoveHandler p.Item1.TextChanged, AddressOf OnMv12Changed
                             RemoveHandler p.Item1.TextChanged, AddressOf OnMv3Changed

                             If isMv3 Then
                                 AddHandler p.Item1.TextChanged, AddressOf OnMv3Changed
                             Else
                                 AddHandler p.Item1.TextChanged, AddressOf OnMv12Changed
                             End If
                         Next
                     End Sub

        For Each g In Groups.Values
            If g Is Nothing Then Continue For
            attach(g.MV1, False)   ' MV1: no compute
            attach(g.MV2, False)   ' MV2: no compute
            attach(g.MV3, True)    ' MV3: compute when row complete
        Next
    End Sub

    ' MV1/MV2 changes: only remember the context; no Excel compute here
    Private Sub OnMv12Changed(sender As Object, e As EventArgs)
        Dim tb = TryCast(sender, TextBox)
        If tb Is Nothing Then Exit Sub
        Dim g As ParamGroup = Nothing
        Dim i As Integer = -1
        If Not ResolveOwner(tb, g, i) Then Exit Sub
        currentGroup = g
        currentRowIdx = i
        ' Optional UX: auto-advance when field becomes non-empty
        'If tb Is currentGroup.MV1(i).tb AndAlso Not String.IsNullOrWhiteSpace(tb.Text) Then
        '    FocusAdvance(currentGroup, i, tb)
        'End If
    End Sub

    ' MV3 changes: when the row has all 3 MVs filled → compute this row immediately
    Private Sub OnMv3Changed(sender As Object, e As EventArgs)
        Dim tb = TryCast(sender, TextBox)
        If tb Is Nothing Then Exit Sub

        Dim g As ParamGroup = Nothing
        Dim i As Integer = -1
        If Not ResolveOwner(tb, g, i) Then Exit Sub
        If g Is Nothing OrElse i < 0 Then Exit Sub

        currentGroup = g
        currentRowIdx = i

        ' Only compute when the row is complete (MV1, MV2, MV3 all have values)
        If Not IsRowComplete(g, i) Then Exit Sub

        ' Target the row of MV3 and compute now
        currentExcelRow = GetRowFromAddr(g.MV3(i).cell)
        ctxDc.TargetRow = currentExcelRow

        ' Single-row compute runs instantly (per your updated StartRowCompute)
        StartRowCompute(g, i)

        ' Optional UX: move focus to next visible row's MV1
        If g.MV1 IsNot Nothing AndAlso i + 1 < g.MV1.Length Then
            Dim nextTb = g.MV1(i + 1).tb
            If nextTb IsNot Nothing AndAlso nextTb.Visible Then
                nextTb.Focus()
                nextTb.SelectAll()
            End If
        End If
    End Sub

    ' Find owner group/row for an MV textbox
    Private Function ResolveOwner(tb As TextBox, ByRef g As ParamGroup, ByRef row As Integer) As Boolean
        g = Nothing : row = -1
        If tb Is Nothing Then Return False
        For Each gg In Groups.Values
            If gg Is Nothing Then Continue For
            For Each arr In New(TextBox, String)()() {gg.MV1, gg.MV2, gg.MV3}
                If arr Is Nothing Then Continue For
                For i As Integer = 0 To arr.Length - 1
                    If arr(i).Item1 Is tb Then g = gg : row = i : Return True
                Next
            Next
        Next
        Return False
    End Function

    ' === TEMP row timing ===
    Private rowStopwatch As System.Diagnostics.Stopwatch = Nothing

    ' Collect per-row elapsed times in order
    Private rowTimes As New List(Of (Key As String, Elapsed As TimeSpan))

#End Region

#Region "Row helpers & visibility" '---------need ko pang iedit kasi meron mga nagaappear na hindi na select sa calibrate

    Private Sub SetRowVisible(g As ParamGroup, idx As Integer, visible As Boolean)
        If g Is Nothing Then Exit Sub

        Dim showLbl = Sub(a As (lbl As Label, cell As String)())
                          If a Is Nothing OrElse idx >= a.Length Then Exit Sub
                          Dim lb = a(idx).lbl
                          If lb IsNot Nothing Then lb.Visible = visible
                      End Sub
        Dim showTb = Sub(a As (tb As TextBox, cell As String)())
                         If a Is Nothing OrElse idx >= a.Length Then Exit Sub
                         Dim tb = a(idx).tb
                         If tb IsNot Nothing Then
                             tb.Visible = visible
                             tb.TabStop = visible
                         End If
                     End Sub
        Dim showOutLbl = Sub(a As (lbl As Label, cell As String)())
                             If a Is Nothing OrElse idx >= a.Length Then Exit Sub
                             Dim lb = a(idx).lbl : If lb IsNot Nothing Then lb.Visible = visible
                         End Sub

        showLbl(g.RangeLabel) : showLbl(g.COL_FUNCTION) : showLbl(g.Nominal) : showLbl(g.Unit) : showLbl(g.Frequency) : showLbl(g.FreqUnit) : showTb(g.MV1) : showTb(g.MV2) : showTb(g.MV3) : showOutLbl(g.Average) : showOutLbl(g.Error) : showOutLbl(g.FinalUncDecl) : showTb(g.Tolerance) : showTb(g.UpperLimit) : showTb(g.LowerLimit) : showTb(g.Remarks)
    End Sub

    Private Sub FocusAdvance(g As ParamGroup, row As Integer, currentTb As WinForms.TextBox)
        Dim target As WinForms.TextBox = Nothing

        If currentTb Is g.MV1(row).tb Then
            target = g.MV2(row).tb
        ElseIf currentTb Is g.MV2(row).tb Then
            target = g.MV3(row).tb
        ElseIf currentTb Is g.MV3(row).tb Then
            ' Move to next row's MV1
            If row + 1 < g.MV1.Length Then
                target = g.MV1(row + 1).tb
            End If
        End If

        If target IsNot Nothing Then
            target.Focus()
            target.SelectAll()
            ScrollIntoViewDeep(target)

            ' --- Auto-scroll to ensure visibility ---
            Dim scrollParent As WinForms.ScrollableControl = TryCast(target.Parent, WinForms.ScrollableControl)
            If scrollParent IsNot Nothing Then
                scrollParent.ScrollControlIntoView(target)
            End If
        End If
    End Sub

    Private Sub ScrollIntoViewDeep(c As WinForms.Control)
        Dim p As WinForms.Control = c
        While p IsNot Nothing
            Dim sc = TryCast(p, WinForms.ScrollableControl)
            If sc IsNot Nothing AndAlso sc.AutoScroll Then
                sc.ScrollControlIntoView(c)
            End If
            p = p.Parent
        End While
    End Sub

    'treat missing controls as blank
    Private Function IsRowComplete(g As ParamGroup, i As Integer) As Boolean
        If g Is Nothing OrElse i < 0 Then Return False
        If g.MV1 Is Nothing OrElse g.MV2 Is Nothing OrElse g.MV3 Is Nothing Then Return False
        If i >= g.MV1.Length OrElse i >= g.MV2.Length OrElse i >= g.MV3.Length Then Return False

        Dim t1 As String = If(g.MV1(i).tb IsNot Nothing, g.MV1(i).tb.Text, "")
        Dim t2 As String = If(g.MV2(i).tb IsNot Nothing, g.MV2(i).tb.Text, "")
        Dim t3 As String = If(g.MV3(i).tb IsNot Nothing, g.MV3(i).tb.Text, "")

        Return Not String.IsNullOrWhiteSpace(t1) AndAlso
           Not String.IsNullOrWhiteSpace(t2) AndAlso
           Not String.IsNullOrWhiteSpace(t3)
    End Function

    Private Function AreAllVisibleRowsComplete() As Boolean
        For Each h In Groups.Values
            If h Is Nothing OrElse h.MV1 Is Nothing Then Continue For
            For i = 0 To h.MV1.Length - 1
                Dim tb1 = h.MV1(i).tb
                Dim tb2 = If(h.MV2 IsNot Nothing AndAlso i < h.MV2.Length, h.MV2(i).tb, Nothing)
                Dim tb3 = If(h.MV3 IsNot Nothing AndAlso i < h.MV3.Length, h.MV3(i).tb, Nothing)
                If tb1 IsNot Nothing AndAlso tb1.Visible Then
                    If tb2 Is Nothing OrElse tb3 Is Nothing Then Return False
                    If String.IsNullOrWhiteSpace(tb1.Text) OrElse
                       String.IsNullOrWhiteSpace(tb2.Text) OrElse
                       String.IsNullOrWhiteSpace(tb3.Text) Then
                        Return False
                    End If
                End If
            Next
        Next
        Return True
    End Function

#End Region

#Region "Excel interop helpers"

    Private Sub WriteInputsRow(ws As Object, g As ParamGroup, i As Integer)
        If ws Is Nothing OrElse g Is Nothing OrElse i < 0 Then Exit Sub
        If g.MV1 IsNot Nothing AndAlso i < g.MV1.Length AndAlso g.MV1(i).tb IsNot Nothing Then
            WriteCell(ws, g.MV1(i).cell, g.MV1(i).tb.Text)
        End If
        If g.MV2 IsNot Nothing AndAlso i < g.MV2.Length AndAlso g.MV2(i).tb IsNot Nothing Then
            WriteCell(ws, g.MV2(i).cell, g.MV2(i).tb.Text)
        End If
        If g.MV3 IsNot Nothing AndAlso i < g.MV3.Length AndAlso g.MV3(i).tb IsNot Nothing Then
            WriteCell(ws, g.MV3(i).cell, g.MV3(i).tb.Text)
        End If
    End Sub

    'null-safe writes; blank/Formula-not-yet-computed returns ""
    Private Sub ReadOutputsRow(ws As Object, g As ParamGroup, i As Integer)
        If ws Is Nothing OrElse g Is Nothing OrElse i < 0 Then Exit Sub

        ' Labels
        If g.Average IsNot Nothing AndAlso i < g.Average.Length Then
            If g.Average(i).lbl IsNot Nothing Then g.Average(i).lbl.Text = ReadCell(ws, g.Average(i).cell)
        End If

        If g.Error IsNot Nothing AndAlso i < g.Error.Length Then
            If g.Error(i).lbl IsNot Nothing Then g.Error(i).lbl.Text = ReadCell(ws, g.Error(i).cell)
        End If

        If g.FinalUncDecl IsNot Nothing AndAlso i < g.FinalUncDecl.Length Then
            If g.FinalUncDecl(i).lbl IsNot Nothing Then g.FinalUncDecl(i).lbl.Text = ReadCell(ws, g.FinalUncDecl(i).cell)
        End If

        ' TextBoxes
        If g.Tolerance IsNot Nothing AndAlso i < g.Tolerance.Length Then
            If g.Tolerance(i).tb IsNot Nothing Then g.Tolerance(i).tb.Text = ReadCell(ws, g.Tolerance(i).cell)
        End If

        If g.UpperLimit IsNot Nothing AndAlso i < g.UpperLimit.Length Then
            If g.UpperLimit(i).tb IsNot Nothing Then g.UpperLimit(i).tb.Text = ReadCell(ws, g.UpperLimit(i).cell)
        End If

        If g.LowerLimit IsNot Nothing AndAlso i < g.LowerLimit.Length Then
            If g.LowerLimit(i).tb IsNot Nothing Then g.LowerLimit(i).tb.Text = ReadCell(ws, g.LowerLimit(i).cell)
        End If

        If g.Remarks IsNot Nothing AndAlso i < g.Remarks.Length Then
            If g.Remarks(i).tb IsNot Nothing Then
                g.Remarks(i).tb.Text = ReadCell(ws, g.Remarks(i).cell)
                ApplyPassFailColor(g.Remarks(i).tb)
            End If
        End If
    End Sub

    Private Function SafeReadCell(ws As Object, addr As String) As String
        If ws Is Nothing OrElse String.IsNullOrWhiteSpace(addr) Then Return ""
        Try
            Dim cell = CallByName(ws, "Range", CallType.Get, addr)
            ' Excel can return Nothing or non-string; coerce safely
            Dim txtObj = CallByName(cell, "Text", CallType.Get)
            Dim txt = TryCast(txtObj, String)
            If txt Is Nothing Then
                ' Some formulas show empty string before inputs; treat as blank
                Return ""
            End If
            Return txt
        Catch
            ' Bad address / not available yet, etc.
            Return ""
        End Try
    End Function

    'Private Sub WriteAllVisibleInputs(ws As Object, g As ParamGroup)
    '    If g Is Nothing OrElse g.MV1 Is Nothing Then Exit Sub
    '    For i As Integer = 0 To g.MV1.Length - 1
    '        Dim tb1 As TextBox = g.MV1(i).tb
    '        If tb1 IsNot Nothing AndAlso tb1.Visible Then WriteInputsRow(ws, g, i)
    '    Next
    'End Sub

    'Private Sub ReadAllOutputsForVisibleRows(ws As Object, g As ParamGroup)
    '    If g Is Nothing OrElse g.MV1 Is Nothing Then Exit Sub
    '    For i As Integer = 0 To g.MV1.Length - 1
    '        Dim tb1 As TextBox = g.MV1(i).tb
    '        If tb1 IsNot Nothing AndAlso tb1.Visible Then ReadOutputsRow(ws, g, i)
    '    Next
    'End Sub

    Private Sub WriteCell(ws As Object, addr As String, value As String)
        Dim cell = CallByName(ws, "Range", CallType.Get, addr)
        CallByName(cell, "Value", CallType.Let, value)
    End Sub

    'same function name, now null-safe + coercion
    Private Function ReadCell(ws As Object, addr As String) As String
        If ws Is Nothing OrElse String.IsNullOrWhiteSpace(addr) Then Return ""
        Try
            Dim cell = CallByName(ws, "Range", CallType.Get, addr)
            If cell Is Nothing Then Return ""
            Dim txtObj = CallByName(cell, "Text", CallType.Get)
            Dim txt = TryCast(txtObj, String)
            If txt Is Nothing Then Return ""
            Return txt
        Catch
            Return ""
        End Try
    End Function

    Private Function GetRowFromAddr(addr As String) As Integer
        If String.IsNullOrWhiteSpace(addr) Then Return -1
        Dim m = System.Text.RegularExpressions.Regex.Match(addr, "\$?[A-Za-z]+\$?(\d+)")
        If Not m.Success Then Return -1
        Return Integer.Parse(m.Groups(1).Value, Globalization.CultureInfo.InvariantCulture)
    End Function

    Private Sub ApplyPassFailColor(tb As WinForms.TextBox)
        If tb Is Nothing Then Exit Sub
        Dim val = If(tb.Text, "").Trim().ToUpperInvariant()
        Select Case val
            Case "PASS"
                tb.BackColor = Drawing.Color.FromArgb(198, 239, 206) ' green
                tb.ForeColor = Drawing.Color.Black
            Case "FAIL"
                tb.BackColor = Drawing.Color.FromArgb(255, 199, 206) ' red
                tb.ForeColor = Drawing.Color.Black
            Case Else
                tb.BackColor = Drawing.SystemColors.ControlLight
                tb.ForeColor = Drawing.SystemColors.WindowText
        End Select
    End Sub

    Private Function NormalizeFile(s As String) As String
        If String.IsNullOrWhiteSpace(s) Then Return "NA"
        For Each ch In Path.GetInvalidFileNameChars()
            s = s.Replace(ch, "_"c)
        Next
        Return s.Trim()
    End Function

#End Region

#Region "Visibility by category + parameter-row filtering"

    Private Sub SetGroupVisible(g As ParamGroup, visible As Boolean)
        If g Is Nothing Then Exit Sub

        Dim setTb = Sub(arr As (tb As TextBox, cell As String)())
                        If arr Is Nothing Then Exit Sub
                        For Each p In arr
                            If p.tb IsNot Nothing Then
                                p.tb.Visible = visible
                                p.tb.TabStop = visible
                                p.tb.ReadOnly = Not visible
                            End If
                        Next
                    End Sub

        Dim setLbl = Sub(arr As (lbl As Label, cell As String)())
                         If arr Is Nothing Then Exit Sub
                         For Each p In arr
                             If p.lbl IsNot Nothing Then p.lbl.Visible = visible
                         Next
                     End Sub

        Dim setPlain = Sub(arr As (lbl As Label, cell As String)())
                           If arr Is Nothing Then Exit Sub
                           For Each t In arr
                               If t.lbl Is Nothing Then Continue For
                               t.lbl.Font = Me.Font
                               t.lbl.ForeColor = SystemColors.ControlText
                           Next
                       End Sub

        setPlain(g.RangeLabel) : setPlain(g.COL_FUNCTION) : setPlain(g.Nominal)
        setPlain(g.Unit) : setPlain(g.Frequency) : setPlain(g.FreqUnit)

        setTb(g.MV1) : setTb(g.MV2) : setTb(g.MV3)
        setLbl(g.Average) : setLbl(g.Error) : setLbl(g.FinalUncDecl)
        setTb(g.Tolerance) : setTb(g.UpperLimit) : setTb(g.LowerLimit) : setTb(g.Remarks)
    End Sub

    Private Function TrimMatch(rx As System.Text.RegularExpressions.Regex, s As String) As String
        Dim m = rx.Match(s)
        If m.Success Then Return m.Groups(1).Value.Trim()
        Return ""
    End Function

    Private Sub ClearArr(ByRef arr As WinForms.Label())
        If arr Is Nothing Then Exit Sub
        For i = 0 To arr.Length - 1
            If arr(i) IsNot Nothing Then arr(i).Text = ""
        Next
    End Sub

#End Region

#Region "Sir Mel"

    ' =========================================================
    '  Camera + Snipping Tool OCR (NO brand/model logic here)
    '  Focus: Normalize OCR text, detect negative signs,
    '         infer READING (main) vs RANGE (scale) from flat text.
    ' =========================================================

    ' ---------- FIELDS ----------
    Dim tentimes As Integer = 0

    ' --- Camera state (same as calibrate) ---
    Private videoSource As AForge.Video.DirectShow.VideoCaptureDevice

    Private latestFrame As Bitmap
    Private latestFrameLock As New Object()

    Dim bmp As Bitmap

    ' --- TEST BURST (3 shots total) ---

    Private testBurstCopiesRemaining As Integer = 0
    Private burstGroup As ParamGroup = Nothing
    Private burstRow As Integer = -1

    ' --- Click-interval state (TEST MODE) ---
    Private lastCaptureAt As DateTime = DateTime.MinValue

    Private lastClickGroup As Object = Nothing   ' ParamGroup
    Private lastClickRow As Integer = -1
    Private lastNextSlot As Integer = 0          ' 1=MV1, 2=MV2, 3=MV3

    ' For thread-safe UI updates in serial receive (kept for compatibility)
    'Delegate Sub SetTextCallback(ByVal [text] As String)

    ' ---------- Win32 Imports ----------
    <DllImport("user32.dll")>
    Private Shared Function ShowWindow(hWnd As IntPtr, nCmdShow As Integer) As Boolean
    End Function

    '<DllImport("user32.dll")>
    'Private Shared Function BlockInput(fBlockIt As Boolean) As Boolean
    'End Function

    Private Const SW_HIDE As Integer = 0
    Private Const SW_SHOW As Integer = 5

    ' ---------- Small helpers ----------
    Private Sub HideSnippingTool()
        Dim snippingProcesses As String() = {"SnippingTool", "SnipAndSketch"}
        For Each procName As String In snippingProcesses
            For Each p As Process In Process.GetProcessesByName(procName)
                Dim h As IntPtr = p.MainWindowHandle
                If h <> IntPtr.Zero Then ShowWindow(h, SW_HIDE)
            Next
        Next
    End Sub

    Private Sub RemoveFocus()
        Dim dummy = Me.Controls("lblDummy")
        If dummy IsNot Nothing Then dummy.Focus()
    End Sub

    Private Sub Captured(ByVal sender As Object, ByVal EventArgs As NewFrameEventArgs)
        bmp = DirectCast(EventArgs.Frame.Clone(), Bitmap)
        PictureBox1.Image = DirectCast(EventArgs.Frame.Clone(), Bitmap)
    End Sub

    ' ---------- Camera preview ----------
    Private Sub Video_NewFrame(sender As Object, eventArgs As AForge.Video.NewFrameEventArgs)
        Dim frame As Bitmap = Nothing
        Try
            frame = DirectCast(eventArgs.Frame.Clone(), Bitmap)

            ' Keep a copy (optional – handy if you also OCR in this form)
            SyncLock latestFrameLock
                If latestFrame IsNot Nothing Then latestFrame.Dispose()
                latestFrame = DirectCast(frame.Clone(), Bitmap)
            End SyncLock

            ' Show in a PictureBox (rename if your control has a different name)
            Dim displayFrame As Bitmap = DirectCast(frame.Clone(), Bitmap)
            If PictureBox1.InvokeRequired Then
                PictureBox1.BeginInvoke(Sub()
                                            If PictureBox1.Image IsNot Nothing Then PictureBox1.Image.Dispose()
                                            PictureBox1.Image = displayFrame
                                        End Sub)
            Else
                If PictureBox1.Image IsNot Nothing Then PictureBox1.Image.Dispose()
                PictureBox1.Image = displayFrame
            End If
        Catch
            ' swallow/optionally log
        Finally
            If frame IsNot Nothing Then frame.Dispose()
        End Try
    End Sub

    Friend WithEvents txtReading As System.Windows.Forms.TextBox

    Private firstClick As Boolean = True
    Private isCapturing As Boolean = False

    Private Sub StopCamera()
        Try
            If videoSource IsNot Nothing Then
                RemoveHandler videoSource.NewFrame, AddressOf VideoSource_NewFrame
                If videoSource.IsRunning Then
                    videoSource.SignalToStop()
                    videoSource.WaitForStop()
                End If
            End If
        Catch
        End Try
    End Sub

    Private Sub VideoSource_NewFrame(sender As Object, eventArgs As AForge.Video.NewFrameEventArgs)
        Dim frame As Bitmap = Nothing
        Try
            frame = DirectCast(eventArgs.Frame.Clone(), Bitmap)

            ' Save latest frame for OCR
            SyncLock latestFrameLock
                If latestFrame IsNot Nothing Then latestFrame.Dispose()
                latestFrame = DirectCast(frame.Clone(), Bitmap)
            End SyncLock

            ' Update PictureBox safely
            Dim displayFrame As Bitmap = DirectCast(frame.Clone(), Bitmap)
            If PictureBox1.InvokeRequired Then
                PictureBox1.BeginInvoke(Sub()
                                            If PictureBox1.Image IsNot Nothing Then PictureBox1.Image.Dispose()
                                            PictureBox1.Image = displayFrame
                                        End Sub)
            Else
                If PictureBox1.Image IsNot Nothing Then PictureBox1.Image.Dispose()
                PictureBox1.Image = displayFrame
            End If
        Catch ex As Exception
            ' Optional: log or debug
        Finally
            If frame IsNot Nothing Then frame.Dispose()
        End Try
    End Sub

    Private Sub checkrange()
        If PictureBox1.Image IsNot Nothing Then
            PictureBox1.Image.Save("C:\Users\dbneri\Documents\Visual Studio 2010\Projects\ASCal\ASCal\bin\Debug\A.jpg", ImageFormat.Jpeg)
        Else
            'kukuha ulit ng picture kasi walang laman yung picturebox1
        End If

        ' --- OCR extraction ---
        DMMtxtparameter.Clear()
        RichTextBox1.Clear()
        DMMrange.Clear()
        RemoveFocus()
        videoSource.Start()
        Try
            Process.Start("C:\Users\dbneri\AppData\Local\Microsoft\WindowsApps\SnippingTool.exe")
            Thread.Sleep(500)
            HideSnippingTool()
            ' Open the image via shortcut (more reliable than Tab-walking)
            My.Computer.Keyboard.SendKeys("^o", True)
            Thread.Sleep(1400)
            My.Computer.Keyboard.SendKeys("A.jpg", True)
            Thread.Sleep(500)
            My.Computer.Keyboard.SendKeys("{ENTER}", True)
            Thread.Sleep(900) ' give it time to load

            ' ===== YOUR INSIDE-SNIPPING SEQUENCE =====
            ' Option A: your tab path1
            My.Computer.Keyboard.SendKeys("{TAB}{TAB}{TAB}{TAB}{TAB}{TAB}{TAB}", True)
            Thread.Sleep(500)
            My.Computer.Keyboard.SendKeys("{ENTER}", True)   ' Text actions
            Thread.Sleep(2000)
            My.Computer.Keyboard.SendKeys("{TAB}{TAB}{TAB}", True)
            Thread.Sleep(500)
            My.Computer.Keyboard.SendKeys("{ENTER}", True)   ' copy all text
            Thread.Sleep(500)
            RichTextBox1.Paste()

            Dim snippingToolProcesses As String() = {"SnippingTool", "SnipAndSketch"} 'close snipping tool
            For Each procName In snippingToolProcesses
                Dim processes As Process() = Process.GetProcessesByName(procName)

                For Each proc In processes
                    Try
                        proc.Kill()
                        proc.WaitForExit()
                        'MessageBox.Show($"{proc.ProcessName} closed successfully.")
                    Catch ex As Exception
                        'MessageBox.Show($"Failed to close {proc.ProcessName}: {ex.Message}")
                    End Try
                Next
            Next
            'after mapaste sa Richtextbox1, kukunin ko lang yung range at parameter (kung V ac ba or dc etc.)
            If RichTextBox1.Text.Contains("114") Then
                RichTextBox1.Text = RichTextBox1.Text.Replace("114", "A")
            End If
            '“V”, “A”, “mA”, “mV”,      “AC”, “DC”, “k”, “M”,       “1000”, “6 00”, “6 0”, “6” and “Manual”
            If RichTextBox1.Text.Contains("mV") Then
                DMMtxtparameter.Text = "mv" 'milli-volt para di magconflict sa V (volt)
            ElseIf RichTextBox1.Text.Contains("V") Then
                DMMtxtparameter.Text = "V"
            ElseIf RichTextBox1.Text.Contains("mA") Then
                DMMtxtparameter.Text = "ma" 'milla-ampere para di magconflict sa A (ampere)
            ElseIf RichTextBox1.Text.Contains("A") Then
                DMMtxtparameter.Text = "A"
            End If
            If RichTextBox1.Text.Contains("AC") Then
                DMMtxtparameter.Text = DMMtxtparameter.Text + "ac" 'ginawang small letter yung ac para di magconflict sa A (ampere)
            ElseIf RichTextBox1.Text.Contains("DC") Then
                DMMtxtparameter.Text = DMMtxtparameter.Text + "dc"
            ElseIf RichTextBox1.Text.Contains("k") Then
                DMMtxtparameter.Text = "k"
            ElseIf RichTextBox1.Text.Contains("M") Then
                DMMtxtparameter.Text = "M"
            End If

            'If RichTextBox1.Text.Contains("Manual") Then
            If RichTextBox1.Text.Contains("1000") Then
                DMMrange.Text = "1000"
            ElseIf RichTextBox1.Text.Contains("6 00") Or RichTextBox1.Text.Contains("600") Then
                DMMrange.Text = "6 00"
            ElseIf RichTextBox1.Text.Contains("6 0") Or RichTextBox1.Text.Contains("60") Then
                DMMrange.Text = "6 0"
            ElseIf RichTextBox1.Text.Contains("6") Then
                DMMrange.Text = "6"
            End If
            'End If
        Finally
            For Each procName In New String() {"SnippingTool", "SnipAndSketch"}
                For Each p As Process In Process.GetProcessesByName(procName)
                    Try : p.Kill() : p.WaitForExit() : Catch : End Try
                Next
            Next
        End Try
        Timercontrolcalib.Start()
    End Sub

    Private Sub checkreading()
        'StopCamera()
        If PictureBox1.Image IsNot Nothing Then
            PictureBox1.Image.Save("C:\Users\dbneri\Documents\Visual Studio 2010\Projects\ASCal\ASCal\bin\Debug\A.jpg", ImageFormat.Jpeg)

            PictureBox1.Image.Save("C:\Users\dbneri\Documents\Visual Studio 2010\Projects\ASCal\ASCal\bin\Debug\" & looping & "A.jpg", ImageFormat.Jpeg)
        Else
            'kukuha ulit ng picture kasi walang laman yung picturebox1
        End If
        ' --- OCR extraction ---
        DMMreading.Clear()
        RichTextBox1.Clear()
        RemoveFocus()

        Try
            ' --- Launch Snipping Tool reliably on Win10/11 ---
            Process.Start("C:\Users\dbneri\AppData\Local\Microsoft\WindowsApps\SnippingTool.exe")
            Thread.Sleep(500)
            HideSnippingTool()
            ' Open the image via shortcut (more reliable than Tab-walking)
            My.Computer.Keyboard.SendKeys("^o", True)
            Thread.Sleep(1400)
            My.Computer.Keyboard.SendKeys("A.jpg", True)
            Thread.Sleep(500)
            My.Computer.Keyboard.SendKeys("{ENTER}", True)
            Thread.Sleep(900) ' give it time to load

            ' ===== YOUR INSIDE-SNIPPING SEQUENCE =====
            ' Option A: your tab path1
            My.Computer.Keyboard.SendKeys("{TAB}{TAB}{TAB}{TAB}{TAB}{TAB}{TAB}", True)
            Thread.Sleep(500)
            My.Computer.Keyboard.SendKeys("{ENTER}", True)   ' Text actions
            Thread.Sleep(2000)
            My.Computer.Keyboard.SendKeys("{TAB}{TAB}{TAB}", True)
            Thread.Sleep(500)
            My.Computer.Keyboard.SendKeys("{ENTER}", True)   ' copy all text
            Thread.Sleep(500)
            RichTextBox1.Paste()
            Dim snippingToolProcesses As String() = {"SnippingTool", "SnipAndSketch"} 'close snipping tool
            videoSource.Start()
            For Each procName In snippingToolProcesses
                Dim processes As Process() = Process.GetProcessesByName(procName)

                For Each proc In processes
                    Try
                        proc.Kill()
                        proc.WaitForExit()
                        'MessageBox.Show($"{proc.ProcessName} closed successfully.")
                    Catch ex As Exception
                        'MessageBox.Show($"Failed to close {proc.ProcessName}: {ex.Message}")
                    End Try
                Next
            Next
            'after mapaste sa Richtextbox1, kukunin ko lang yung range at parameter (kung V ac ba or dc etc.)
            RichTextBox1.Text = RichTextBox1.Text.Replace(",", ".")
            RichTextBox1.Text = RichTextBox1.Text.Replace("0 ", "A")
            '“V”, “A”, “mA”, “mV”,      “AC”, “DC”, “k”, “M”,       “1000”, “6 00”, “6 0”, “6” and “Manual”
            If RichTextBox1.Text.Contains("114") Then
                RichTextBox1.Text = RichTextBox1.Text.Replace("114", "A")
            End If
            If RichTextBox1.Text.Contains("6 00") Then
                RichTextBox1.Text = RichTextBox1.Text.Replace("6 00", "A")
            End If
            If RichTextBox1.Text.Contains("6 0") Then
                RichTextBox1.Text = RichTextBox1.Text.Replace("6 0", "A")
            End If
            If RichTextBox1.Text.Contains("6") Then
                RichTextBox1.Text = RichTextBox1.Text.Replace("6", "A")
            End If
            If RichTextBox1.Text.Contains("-") Then 'if looping is for -600V huwag papasukin
                If Not (looping = 13 Or looping = 14 Or looping = 15) Then
                    RichTextBox1.Text = RichTextBox1.Text.Replace("-", "A")
                End If
            End If
            If RichTextBox1.Text.Contains("+") Then
                RichTextBox1.Text = RichTextBox1.Text.Replace("+", "A")
            End If
            dec = 32
            While dec <= 43
                If RichTextBox1.Text.Contains(ChrW(dec)) Then
                    RichTextBox1.Text = RichTextBox1.Text.Replace(ChrW(dec), "A")
                End If
                dec = dec + 1
            End While
            If RichTextBox1.Text.Contains(ChrW(47)) Then
                RichTextBox1.
                    Text = RichTextBox1.Text.Replace(ChrW(47), "A")
            End If
            dec = 58
            While dec <= 64
                If RichTextBox1.Text.Contains(ChrW(dec)) Then
                    RichTextBox1.Text = RichTextBox1.Text.Replace(ChrW(dec), "A")
                End If
                dec = dec + 1
            End While
            dec = 91
            While dec <= 96
                If RichTextBox1.Text.Contains(ChrW(dec)) Then
                    RichTextBox1.Text = RichTextBox1.Text.Replace(ChrW(dec), "A")
                End If
                dec = dec + 1
            End While
            dec = 123
            While dec <= 255
                If RichTextBox1.Text.Contains(ChrW(dec)) Then
                    RichTextBox1.Text = RichTextBox1.Text.Replace(ChrW(dec), "A")
                End If
                dec = dec + 1
            End While
            RichTextBox1.Text = Regex.Replace(RichTextBox1.Text, "(?<!\S)0(?!\S)", "")
            RichTextBox1.Text = Regex.Replace(RichTextBox1.Text, "\s{2,}", " ").Trim()
            RichTextBox1.Text = RichTextBox1.Text.Replace(" ", "A")
            RichTextBox1.Text = RichTextBox1.Text.Replace(vbCr, "A")
            RichTextBox1.Text = RichTextBox1.Text.Replace(vbNewLine, "A")
            RichTextBox1.Text = RemoveAlphabets(RichTextBox1.Text)
            If getwireresistance = 1 Then 'meaning kukuha ng resistance ng wire
                DMMreading.Text = RichTextBox1.Text
                If DMMreading.Text = Nothing Or DMMreading.Text.Contains("..") Or DMMreading.Text.Contains(". ") Then
                    getwireresistance = 1 'magread ulit ng wire resistance
                ElseIf Not DMMreading.Text.Contains("0") And Not DMMreading.Text.Contains("1") And Not DMMreading.Text.Contains("2") And Not DMMreading.Text.Contains("3") And Not DMMreading.Text.Contains("4") And Not DMMreading.Text.Contains("5") And Not DMMreading.Text.Contains("6") And Not DMMreading.Text.Contains("7") And Not DMMreading.Text.Contains("8") And Not DMMreading.Text.Contains("9") Then
                    getwireresistance = 1 'magread ulit ng wire resistance
                ElseIf Not DMMreading.Text.Contains(".") Then
                    getwireresistance = 1 'magread ulit ng wire resistance
                Else
                    getwireresistance = 2
                    wireresistance = DMMreading.Text
                End If
            ElseIf getwireresistance = 2 Then 'kapag 2 na ibig sabihin nakuha na value ng wire resistance at lagi na iminus sa reading
                DMMreading.Text = RichTextBox1.Text - wireresistance
            Else 'getwireresistance = 0
                DMMreading.Text = RichTextBox1.Text
                If DMMreading.Text = Nothing Or DMMreading.Text.Contains("..") Or DMMreading.Text.Contains(". ") Then
                    malingreading = 1
                    TextBox3.Text = malingreading
                ElseIf Not DMMreading.Text.Contains("0") And Not DMMreading.Text.Contains("1") And Not DMMreading.Text.Contains("2") And Not DMMreading.Text.Contains("3") And Not DMMreading.Text.Contains("4") And Not DMMreading.Text.Contains("5") And Not DMMreading.Text.Contains("6") And Not DMMreading.Text.Contains("7") And Not DMMreading.Text.Contains("8") And Not DMMreading.Text.Contains("9") Then
                    malingreading = 1
                    TextBox3.Text = malingreading
                ElseIf Not DMMreading.Text.Contains(".") Then
                    malingreading = 1
                    TextBox3.Text = malingreading
                Else
                    malingreading = 0
                    TextBox3.Text = malingreading
                End If
            End If
        Finally
            For Each procName In New String() {"SnippingTool", "SnipAndSketch"}
                For Each p As Process In Process.GetProcessesByName(procName)
                    Try : p.Kill() : p.WaitForExit() : Catch : End Try
                Next
            Next
        End Try
    End Sub

    ' Single entry-point: lahat ng logic nasa loob lang ng function na ’to.
    ' Goal:
    '  - 1st call → ilagay DMMreading.Text sa MV1(r)
    '  - 2nd call → MV2(r)
    '  - 3rd call → MV3(r) tapos mag-compute ng uncertainty
    '  - Pag tapos ng MV3, lilipat sa next row
    Private Sub inputReadingToMvTextbox()

        ' ==== Guards: Check kung may groups at may reading, kung wala, exit agad ====
        If Groups Is Nothing OrElse Groups.Count = 0 Then
            Debug.WriteLine("inputReadingToMvTextbox: No groups found.")
            Exit Sub
        End If
        If DMMreading Is Nothing OrElse String.IsNullOrWhiteSpace(DMMreading.Text) Then
            Debug.WriteLine("inputReadingToMvTextbox: No DMM reading.")
            Exit Sub
        End If
        ' Store the reading from DMM into a variable (para maging string)
        Dim reading As String = DMMreading.Text

        ' ==== Sticky State (persistent state across calls) ====
        Static capturePhase As Integer = 1  ' 1 → MV1, 2 → MV2, 3 → MV3
        Static curGroup As ParamGroup = Nothing
        Static curRow As Integer = -1

        ' Helper function to process the groups. 'orderedGroups' ay galing sa Groups data structure
        ' In this list, we're ensuring that we follow the order of the groups (no alphabetic sorting).
        Dim orderedGroups As List(Of ParamGroup) = Groups.Values.ToList()

        ' Function to get the maximum number of rows from MV1, MV2, MV3. To avoid looping beyond valid rows.
        Dim MaxRowsIn As Func(Of ParamGroup, Integer) =
        Function(g As ParamGroup) As Integer
            Return Math.Max(Math.Max(If(g.MV1?.Length, 0), If(g.MV2?.Length, 0)), If(g.MV3?.Length, 0))
        End Function

        ' Function to get the appropriate TextBox for each slot (MV1, MV2, MV3) based on the row and phase
        ' In short, we are selecting which textbox we will input data into based on the "capturePhase".
        Dim GetMVTextboxSafe As Func(Of ParamGroup, Integer, Integer, TextBox) =
        Function(g As ParamGroup, r As Integer, slot As Integer) As TextBox
            Try
                Select Case slot
                    Case 1 : If g IsNot Nothing AndAlso g.MV1 IsNot Nothing AndAlso r >= 0 AndAlso r < g.MV1.Length Then Return g.MV1(r).tb
                    Case 2 : If g IsNot Nothing AndAlso g.MV2 IsNot Nothing AndAlso r >= 0 AndAlso r < g.MV2.Length Then Return g.MV2(r).tb
                    Case 3 : If g IsNot Nothing AndAlso g.MV3 IsNot Nothing AndAlso r >= 0 AndAlso r < g.MV3.Length Then Return g.MV3(r).tb
                End Select
            Catch ex As Exception
                Debug.WriteLine("Error in GetMVTextboxSafe: " & ex.Message)
            End Try
            Return Nothing
        End Function

        ' Function to search for the first empty slot (MV1 → MV2 → MV3) in a specific group and row
        Dim SearchInGroup As Func(Of ParamGroup, Integer, (Boolean, Integer, Integer, TextBox)) =
        Function(g As ParamGroup, r0 As Integer)
            Dim mr As Integer = MaxRowsIn(g)
            For rr = Math.Max(0, r0) To mr - 1
                ' Trying to find the first empty textbox in MV1, MV2, or MV3
                If g.MV1 IsNot Nothing AndAlso rr < g.MV1.Length Then
                    Dim t As TextBox = g.MV1(rr).tb
                    If t IsNot Nothing AndAlso t.Visible AndAlso String.IsNullOrWhiteSpace(t.Text) Then Return (True, rr, 1, t)
                End If
                If g.MV2 IsNot Nothing AndAlso rr < g.MV2.Length Then
                    Dim t As TextBox = g.MV2(rr).tb
                    If t IsNot Nothing AndAlso t.Visible AndAlso String.IsNullOrWhiteSpace(t.Text) Then Return (True, rr, 2, t)
                End If
                If g.MV3 IsNot Nothing AndAlso rr < g.MV3.Length Then
                    Dim t As TextBox = g.MV3(rr).tb
                    If t IsNot Nothing AndAlso t.Visible AndAlso String.IsNullOrWhiteSpace(t.Text) Then Return (True, rr, 3, t)
                End If
            Next
            Return (False, -1, 0, Nothing)
        End Function

        ' Function to find the next available empty slot across groups, with wrap-around.
        Dim FindNextEmpty As Func(Of ParamGroup, Integer, (Boolean, Integer, Integer, TextBox, ParamGroup)) =
        Function(g0 As ParamGroup, startRow As Integer)
            ' Search in the current group
            Dim hit = SearchInGroup(g0, startRow)
            If hit.Item1 Then Return (True, hit.Item2, hit.Item3, hit.Item4, g0)

            ' If no empty slot found, search in the following groups
            Dim idx As Integer = orderedGroups.IndexOf(g0)
            If idx < 0 Then idx = 0  ' Default to first group if current group not found
            For i = idx + 1 To orderedGroups.Count - 1
                Dim g As ParamGroup = orderedGroups(i)
                hit = SearchInGroup(g, 0)  ' Start search from the first row of the next group
                If hit.Item1 Then Return (True, hit.Item2, hit.Item3, hit.Item4, g)
            Next

            ' If still no empty slot, wrap-around to the first group
            For i = 0 To idx - 1
                Dim g As ParamGroup = orderedGroups(i)
                hit = SearchInGroup(g, 0)
                If hit.Item1 Then Return (True, hit.Item2, hit.Item3, hit.Item4, g)
            Next

            ' If no empty slot found after searching all groups, return failure
            Return (False, -1, 0, Nothing, Nothing)
        End Function

        ' Function to check if all MV slots in the row are filled (complete row)
        Dim RowComplete As Func(Of ParamGroup, Integer, Boolean) =
        Function(g As ParamGroup, r As Integer) As Boolean
            ' To check if the current row is complete (i.e., no empty slots)
            Dim allFilled As Boolean = True  ' Assume all slots are filled
            If g.MV1 IsNot Nothing AndAlso r < g.MV1.Length AndAlso g.MV1(r).tb IsNot Nothing Then
                If String.IsNullOrWhiteSpace(g.MV1(r).tb.Text) Then allFilled = False  ' If MV1 is empty, mark as not filled
            End If
            If g.MV2 IsNot Nothing AndAlso r < g.MV2.Length AndAlso g.MV2(r).tb IsNot Nothing Then
                If String.IsNullOrWhiteSpace(g.MV2(r).tb.Text) Then allFilled = False  ' If MV2 is empty, mark as not filled
            End If
            If g.MV3 IsNot Nothing AndAlso r < g.MV3.Length AndAlso g.MV3(r).tb IsNot Nothing Then
                If String.IsNullOrWhiteSpace(g.MV3(r).tb.Text) Then allFilled = False  ' If MV3 is empty, mark as not filled
            End If
            Return allFilled  ' Return true if all MV slots are filled, false otherwise
        End Function

        ' ==== Initialize focus on first row if needed ====
        If curGroup Is Nothing OrElse curRow < 0 Then
            ' Kung walang group or row, magsisimula tayo sa unang row ng unang group
            Dim g0 As ParamGroup = orderedGroups.FirstOrDefault()
            If g0 Is Nothing Then Exit Sub
            Dim init = FindNextEmpty(g0, 0)
            If Not init.Item1 Then Exit Sub
            curGroup = init.Item5
            curRow = init.Item2
            capturePhase = 1  ' Start with MV1
        End If

        ' ==== Strict sequence for filling MV1 → MV2 → MV3 ====
        ' Piliin ang tamang TextBox na i-fill based on capturePhase (MV1, MV2, MV3)
        Dim targetTB As TextBox = GetMVTextboxSafe(curGroup, curRow, capturePhase)

        ' ==== Write the reading to the selected TextBox ====
        ' Kung may target textbox, i-assign yung reading sa textbox
        If targetTB IsNot Nothing AndAlso targetTB.Visible Then
            If targetTB.InvokeRequired Then
                targetTB.Invoke(Sub() targetTB.Text = reading)
            Else
                targetTB.Text = reading
            End If
        End If

        ' ==== Compute uncertainty if row is complete ====
        ' Kung kumpleto na yung row (lahat ng MV slots ay may laman), tatawagin ang uncertainty computation
        If RowComplete(curGroup, curRow) Then
            StartRowCompute(curGroup, curRow)  ' Uncertainty computation after MV3
        End If

        ' ==== Advance phase and possibly row ====
        capturePhase = If(capturePhase = 3, 1, capturePhase + 1)  ' Pag tapos sa MV3, mag-move to next row (MV1)

        ' Pagkatapos ng MV3, move to the next row (MV1)
        If capturePhase = 1 Then
            Dim hop = FindNextEmpty(curGroup, curRow + 1)
            If Not hop.Item1 Then hop = FindNextEmpty(curGroup, 0)
            If hop.Item1 Then
                curGroup = hop.Item5
                curRow = hop.Item2
            End If
        End If
    End Sub

    Private Sub sapialarm()

        sapi = CreateObject("Sapi.spvoice")
        sapi.Speak("Wrong range or parameter! Operation Stop.")

        sapi = CreateObject("Sapi.spvoice")
        sapi.Speak("Wrong range or parameter! Operation Stop.")

        sapi = CreateObject("Sapi.spvoice")
        sapi.Speak("Wrong range or parameter! Operation Stop.")
        looping = 0
    End Sub

    Private Sub BtnCapture_Click(sender As Object, e As EventArgs) Handles BtnCapture.Click
        For r As Integer = 1 To 26
            For i As Integer = 1 To 3
                DMMreading.Text = "0.001"
                malingreading = 0
                inputReadingToMvTextbox()
            Next
        Next

        SimpleExportExcel()

        '' --- INLINE CHECK BEFORE EXPORT (same logic, no new function) ---
        'Dim totalVisible As Integer = 0
        'Dim totalFilled As Integer = 0
        'If Groups IsNot Nothing AndAlso Groups.Count > 0 Then
        '    For Each g In Groups.Values
        '        If g Is Nothing Then Continue For
        '        Dim maxRows As Integer = Math.Max(Math.Max(If(g.MV1?.Length, 0), If(g.MV2?.Length, 0)), If(g.MV3?.Length, 0))
        '        For i As Integer = 0 To maxRows - 1
        '            Dim t1 As TextBox = If(g.MV1 IsNot Nothing AndAlso i < g.MV1.Length, g.MV1(i).tb, Nothing)
        '            Dim t2 As TextBox = If(g.MV2 IsNot Nothing AndAlso i < g.MV2.Length, g.MV2(i).tb, Nothing)
        '            Dim t3 As TextBox = If(g.MV3 IsNot Nothing AndAlso i < g.MV3.Length, g.MV3(i).tb, Nothing)

        '            If t1 IsNot Nothing AndAlso t1.Visible Then
        '                totalVisible += 1
        '                If Not String.IsNullOrWhiteSpace(t1.Text) Then totalFilled += 1
        '            End If
        '            If t2 IsNot Nothing AndAlso t2.Visible Then
        '                totalVisible += 1
        '                If Not String.IsNullOrWhiteSpace(t2.Text) Then totalFilled += 1
        '            End If
        '            If t3 IsNot Nothing AndAlso t3.Visible Then
        '                totalVisible += 1
        '                If Not String.IsNullOrWhiteSpace(t3.Text) Then totalFilled += 1
        '            End If
        '        Next
        '    Next
        'End If
        'TextBox1.Text = looping
        'If looping = 1 Then
        '    If wrongrangeparameter = 0 And malingreading = 0 Then
        '        SerialPort1.Write("R225rC2c") 'para tumapat sa Vdc yung DMM
        '        Thread.Sleep(3000) 'delay 3 seconds
        '        SerialPort1.Write("X3000Y1P1RF") 'para pindutin yung range 1 time
        '        Timer1.Interval = 7000
        '    End If
        '    Timer1.Start()
        'ElseIf looping = 7 Then
        '    If wrongrangeparameter = 0 And malingreading = 0 Then
        '        SerialPort1.Write("X3000Y1P1RF") 'para pindutin yung range 1 time
        '        Timer1.Interval = 7000
        '    End If
        '    Timer1.Start()
        'ElseIf looping = 10 Then
        '    If wrongrangeparameter = 0 And malingreading = 0 Then
        '        SerialPort1.Write("X3000Y1P1RF") 'para pindutin yung range 1 time
        '        Timer1.Interval = 7000
        '    End If
        '    Timer1.Start()
        'ElseIf looping = 16 Then 'umulit
        '    If wrongrangeparameter = 0 And malingreading = 0 Then
        '        SerialPort1.Write("R225rC1c") 'para tumapat sa Vdc yung DMM
        '        Thread.Sleep(3000) 'delay 3 seconds
        '        SerialPort1.Write("X3800Y1P1RF") 'para pindutin yung yellow button 1 time
        '        Timer1.Interval = 7000
        '    End If
        '    Timer1.Start()
        'ElseIf looping = 22 Then
        '    If wrongrangeparameter = 0 And malingreading = 0 Then
        '        SerialPort1.Write("R225rW4w") 'para tumapat sa Vdc yung DMM
        '        Timer1.Interval = 3000
        '    End If
        '    Timer1.Start()
        'ElseIf looping = 31 Then
        '    If wrongrangeparameter = 0 And malingreading = 0 Then
        '        SerialPort1.Write("R225rC2c") 'para tumapat sa Vdc yung DMM
        '        Thread.Sleep(3000) 'delay 3 seconds
        '        SerialPort1.Write("X3000Y1P1RF") 'para pindutin yung range 1 time
        '        Timer1.Interval = 7000
        '    End If
        '    Timer1.Start()
        'ElseIf looping = 37 Then
        '    If wrongrangeparameter = 0 And malingreading = 0 Then
        '        SerialPort1.Write("X3000Y1P1RF") 'para pindutin yung range 1 time
        '        Timer1.Interval = 7000
        '    End If
        '    Timer1.Start()
        'ElseIf looping = 43 Then
        '    If wrongrangeparameter = 0 And malingreading = 0 Then
        '        SerialPort1.Write("X3000Y1P1RF") 'para pindutin yung range 1 time
        '        Timer1.Interval = 7000
        '    End If
        '    Timer1.Start()
        'ElseIf looping = 49 Then
        '    If wrongrangeparameter = 0 And malingreading = 0 Then
        '        SerialPort1.Write("R225rC2c") 'para tumapat sa Vdc yung DMM
        '        Timer1.Interval = 3000
        '    End If
        '    Timer1.Start()
        'ElseIf looping = 55 Then
        '    If wrongrangeparameter = 0 And malingreading = 0 Then
        '        SerialPort1.Write("R225rC1c") 'para tumapat sa ohms yung DMM
        '        Thread.Sleep(3000) 'delay 3 seconds
        '        SerialPort1.Write("X3000Y1P2RF") 'para pindutin yung range 2 time
        '        Timer1.Interval = 7000
        '    End If
        '    Timer1.Start()
        'ElseIf looping = 61 Then
        '    If wrongrangeparameter = 0 And malingreading = 0 Then
        '        SerialPort1.Write("X3000Y1P1RF") 'para pindutin yung range 1 time
        '        Timer1.Interval = 7000
        '    End If
        '    Timer1.Start()
        'ElseIf looping = 64 Then
        '    If wrongrangeparameter = 0 And malingreading = 0 Then
        '        SerialPort1.Write("X3000Y1P1RF") 'para pindutin yung range 1 time
        '        Timer1.Interval = 7000
        '    End If
        '    Timer1.Start()
        'ElseIf looping = 67 Then
        '    If wrongrangeparameter = 0 And malingreading = 0 Then
        '        SerialPort1.Write("X3000Y1P1RF") 'para pindutin yung range 1 time
        '        Timer1.Interval = 7000
        '    End If
        '    Timer1.Start()
        'ElseIf looping = 70 Then
        '    If wrongrangeparameter = 0 And malingreading = 0 Then
        '        SerialPort1.Write("X3000Y1P1RF") 'para pindutin yung range 1 time
        '        Timer1.Interval = 7000
        '    End If
        '    Timer1.Start()
        'ElseIf looping = 73 Then
        '    If wrongrangeparameter = 0 And malingreading = 0 Then
        '        SerialPort1.Write("X3000Y1P1RF") 'para pindutin yung range 1 time
        '        Timer1.Interval = 7000
        '    End If
        '    Timer1.Start()
        'Else
        '    Timercontrolcalib.Interval = 100
        '    Timercontrolcalib.Start()
        'End If

    End Sub

    ' Resolve the ParamGroup/row from a focused TextBox control.
    Private Function TryResolveFocus(ByRef outGroup As ParamGroup, ByRef outRow As Integer, Optional ctrl As Control = Nothing) As Boolean
        outGroup = Nothing : outRow = -1
        If ctrl Is Nothing Then ctrl = Me.ActiveControl
        Dim tb As TextBox = TryCast(ctrl, TextBox)
        If tb Is Nothing Then Return False

        For Each pg As ParamGroup In Groups.Values
            If pg Is Nothing Then Continue For
            Dim n As Integer = Math.Max(Math.Max(If(pg.MV1?.Length, 0), If(pg.MV2?.Length, 0)), If(pg.MV3?.Length, 0))
            For i As Integer = 0 To n - 1
                If (pg.MV1 IsNot Nothing AndAlso i < pg.MV1.Length AndAlso pg.MV1(i).tb Is tb) OrElse
               (pg.MV2 IsNot Nothing AndAlso i < pg.MV2.Length AndAlso pg.MV2(i).tb Is tb) OrElse
               (pg.MV3 IsNot Nothing AndAlso i < pg.MV3.Length AndAlso pg.MV3(i).tb Is tb) Then
                    outGroup = pg : outRow = i
                    Return True
                End If
            Next
        Next
        Return False
    End Function

    ' Global variable to track focused textbox
    Private currentFocusedTextBox As TextBox

    ' Event handler to track focus change
    Private Sub TextBox_Enter(sender As Object, e As EventArgs)
        currentFocusedTextBox = CType(sender, TextBox)
    End Sub


    Function RemoveAlphabets(ByVal str As String) As String
        Dim output As String = ""
        For Each ch As Char In str
            ' Check if the character is NOT a letter
            If Not Char.IsLetter(ch) Then
                output &= ch
            End If
        Next
        Return output
    End Function

    Private Sub KryptonWebBrowser1_DocumentCompleted(sender As Object, e As WebBrowserDocumentCompletedEventArgs) Handles KryptonWebBrowser1.DocumentCompleted

    End Sub

    Private Sub Timer1_Tick(sender As Object, e As EventArgs) Handles Timer1.Tick
        Timer1.Stop()
        checkrange()
    End Sub

    Private Sub Timercontrolcalib_Tick(sender As Object, e As EventArgs) Handles Timercontrolcalib.Tick
        Timercontrolcalib.Stop()
        If looping >= 1 And looping <= 3 Then
            If DMMtxtparameter.Text.Contains("Vdc") AndAlso DMMrange.Text.Contains("6") Then
                wrongrangeparameter = 0
                TextBox2.Text = wrongrangeparameter
                SerialPort1.Write("OOUT 0V,0HZX")
                Thread.Sleep(500)
                SerialPort1.Write("OOPERX") 'calibrator operate
                SerialPort1.Write("!")
                If looping = 1 Then
                    Timeronepoint5.Interval = 3000
                Else
                    Timeronepoint5.Interval = 300
                End If
                Timeronepoint5.Start()
            Else
                wrongrangeparameter = wrongrangeparameter + 1
                TextBox2.Text = wrongrangeparameter
                If wrongrangeparameter >= 3 Then
                    sapialarm()
                Else
                    looping = 0
                    TextBox1.Text = looping
                End If
                Timer2.Start()
            End If
        ElseIf looping >= 4 AndAlso looping <= 6 Then
            If DMMtxtparameter.Text.Contains("Vdc") AndAlso DMMrange.Text.Contains("6") Then
                wrongrangeparameter = 0
                TextBox2.Text = wrongrangeparameter
                SerialPort1.Write("OOUT 5V,0HZX") '5v
                Thread.Sleep(500)
                SerialPort1.Write("OOPERX") 'calibrator operate
                SerialPort1.Write("!")
                If looping = 4 Then
                    Timeronepoint5.Interval = 4000
                Else
                    Timeronepoint5.Interval = 300
                End If
                Timeronepoint5.Start()
            Else
                wrongrangeparameter = wrongrangeparameter + 1
                TextBox2.Text = wrongrangeparameter
                If wrongrangeparameter >= 3 Then
                    sapialarm()
                Else
                    looping = 3
                    TextBox1.Text = looping
                End If
                Timer2.Start()
            End If
        ElseIf looping >= 7 AndAlso looping <= 9 Then
            If DMMtxtparameter.Text.Contains("Vdc") AndAlso (DMMrange.Text.Contains("6 0") Or DMMrange.Text.Contains("60")) Then
                wrongrangeparameter = 0
                TextBox2.Text = wrongrangeparameter
                SerialPort1.Write("OOUT 50V,0HZX") '50v
                Thread.Sleep(500)
                SerialPort1.Write("OOPERX") 'calibrator operate
                SerialPort1.Write("!")
                If looping = 7 Then
                    Timeronepoint5.Interval = 5000
                Else
                    Timeronepoint5.Interval = 300
                End If
                Timeronepoint5.Start()
            Else
                wrongrangeparameter = wrongrangeparameter + 1
                TextBox2.Text = wrongrangeparameter
                If wrongrangeparameter >= 3 Then
                    sapialarm()
                Else
                    looping = 6
                    TextBox1.Text = looping
                End If
                Timer2.Start()
            End If
        ElseIf looping >= 10 AndAlso looping <= 12 Then
            If DMMtxtparameter.Text.Contains("Vdc") AndAlso DMMrange.Text.Contains("6 00") Then
                wrongrangeparameter = 0
                TextBox2.Text = wrongrangeparameter
                SerialPort1.Write("OOUT 600V,0HZX") '600v
                Thread.Sleep(500)
                SerialPort1.Write("OOPERX") 'calibrator operate
                SerialPort1.Write("!")
                If looping = 10 Then
                    Timeronepoint5.Interval = 10000
                Else
                    Timeronepoint5.Interval = 300
                End If
                Timeronepoint5.Start()
            Else
                wrongrangeparameter = wrongrangeparameter + 1
                TextBox2.Text = wrongrangeparameter
                If wrongrangeparameter >= 3 Then
                    sapialarm()
                Else
                    looping = 9
                    TextBox1.Text = looping
                End If
                Timer2.Start()
            End If
        ElseIf looping >= 13 AndAlso looping <= 15 Then
            If DMMtxtparameter.Text.Contains("Vdc") AndAlso DMMrange.Text.Contains("6 00") Then
                wrongrangeparameter = 0
                TextBox2.Text = wrongrangeparameter
                SerialPort1.Write("OOUT -600V,0HZX") '-600v
                Thread.Sleep(500)
                SerialPort1.Write("OOPERX") 'calibrator operate
                SerialPort1.Write("!")
                If looping = 13 Then
                    Timeronepoint5.Interval = 10000
                Else
                    Timeronepoint5.Interval = 300
                End If
                Timeronepoint5.Start()
            Else
                wrongrangeparameter = wrongrangeparameter + 1
                TextBox2.Text = wrongrangeparameter
                If wrongrangeparameter >= 3 Then
                    sapialarm()
                Else
                    looping = 12
                    TextBox1.Text = looping
                End If
                Timer2.Start()
            End If
        ElseIf looping >= 16 AndAlso looping <= 18 Then
            If DMMtxtparameter.Text.Contains("mvdc") AndAlso (DMMrange.Text.Contains("6 00") Or DMMrange.Text.Contains("600")) Then
                wrongrangeparameter = 0
                TextBox2.Text = wrongrangeparameter
                SerialPort1.Write("OOUT 10mV,0HZX")
                Thread.Sleep(500)
                SerialPort1.Write("OOPERX") 'calibrator operate
                SerialPort1.Write("!")
                If looping = 16 Then
                    Timeronepoint5.Interval = 3000
                Else
                    Timeronepoint5.Interval = 300
                End If
                Timeronepoint5.Start()
            Else
                wrongrangeparameter = wrongrangeparameter + 1
                TextBox2.Text = wrongrangeparameter
                If wrongrangeparameter >= 3 Then
                    sapialarm()
                Else
                    looping = 15
                    TextBox1.Text = looping
                End If
                Timer2.Start()
            End If
        ElseIf looping >= 19 AndAlso looping <= 21 Then
            If DMMtxtparameter.Text.Contains("mvdc") AndAlso DMMrange.Text.Contains("6 00") Then
                wrongrangeparameter = 0
                TextBox2.Text = wrongrangeparameter
                SerialPort1.Write("OOUT 600mV,0HZX")
                Thread.Sleep(500)
                SerialPort1.Write("OOPERX") 'calibrator operate
                SerialPort1.Write("!")
                If looping = 19 Then
                    Timeronepoint5.Interval = 3000
                Else
                    Timeronepoint5.Interval = 300
                End If
                Timeronepoint5.Start()
            Else
                wrongrangeparameter = wrongrangeparameter + 1
                TextBox2.Text = wrongrangeparameter
                If wrongrangeparameter >= 3 Then
                    sapialarm()
                Else
                    looping = 18
                    TextBox1.Text = looping
                End If
                Timer2.Start()
            End If
        ElseIf looping >= 22 AndAlso looping <= 24 Then
            wrongrangeparameter = 0
            TextBox2.Text = wrongrangeparameter
            SerialPort1.Write("OOUT 0.5V,45HZX")
            Thread.Sleep(500)
            SerialPort1.Write("OOPERX") 'calibrator operate
            SerialPort1.Write("!")
            If looping = 22 Then
                Timeronepoint5.Interval = 10000
            Else
                Timeronepoint5.Interval = 300
            End If
            Timeronepoint5.Start()
        ElseIf looping >= 25 AndAlso looping <= 27 Then
            wrongrangeparameter = 0
            TextBox2.Text = wrongrangeparameter
            SerialPort1.Write("OOUT 0.5V,0HZX")
            Thread.Sleep(500)
            SerialPort1.Write("OOPERX") 'calibrator operate
            SerialPort1.Write("!")
            If looping = 25 Then
                Timeronepoint5.Interval = 3000
            Else
                Timeronepoint5.Interval = 300
            End If
            Timeronepoint5.Start()
        ElseIf looping >= 28 AndAlso looping <= 30 Then
            wrongrangeparameter = 0
            TextBox2.Text = wrongrangeparameter
            SerialPort1.Write("OOUT 250V,500HZX") '500v
            Thread.Sleep(5000)
            SerialPort1.Write("OOUT 300V,500HZX") '500v
            Thread.Sleep(5000)
            SerialPort1.Write("OOUT 350V,500HZX") '500v
            Thread.Sleep(5000)
            SerialPort1.Write("OOUT 400V,500HZX") '500v
            Thread.Sleep(5000)
            SerialPort1.Write("OOUT 450V,500HZX") '500v
            Thread.Sleep(5000)
            SerialPort1.Write("OOUT 500V,500HZX") '500v
            Thread.Sleep(5000)
            SerialPort1.Write("OOPERX") 'calibrator operate
            SerialPort1.Write("!")
            If looping = 28 Then
                Timeronepoint5.Interval = 10000
            Else
                Timeronepoint5.Interval = 300
            End If
            Timeronepoint5.Start()
        ElseIf looping >= 31 AndAlso looping <= 33 Then
            If DMMtxtparameter.Text.Contains("Vac") AndAlso DMMrange.Text.Contains("6") Then
                wrongrangeparameter = 0
                TextBox2.Text = wrongrangeparameter
                SerialPort1.Write("OOUT 5V,45HZX")
                Thread.Sleep(500)
                SerialPort1.Write("OOPERX") 'calibrator operate
                SerialPort1.Write("!")
                If looping = 31 Then
                    Timeronepoint5.Interval = 10000
                Else
                    Timeronepoint5.Interval = 300
                End If
                Timeronepoint5.Start()
            Else
                wrongrangeparameter = wrongrangeparameter + 1
                TextBox2.Text = wrongrangeparameter
                If wrongrangeparameter >= 3 Then
                    sapialarm()
                Else
                    looping = 30
                    TextBox1.Text = looping
                End If
                Timer2.Start()
            End If
        ElseIf looping >= 34 AndAlso looping <= 36 Then
            If DMMtxtparameter.Text.Contains("Vac") AndAlso DMMrange.Text.Contains("6") Then
                wrongrangeparameter = 0
                TextBox2.Text = wrongrangeparameter
                SerialPort1.Write("OOUT 5V,1KHZX")
                Thread.Sleep(500)
                SerialPort1.Write("OOPERX") 'calibrator operate
                SerialPort1.Write("!")
                If looping = 34 Then
                    Timeronepoint5.Interval = 10000
                Else
                    Timeronepoint5.Interval = 300
                End If
                Timeronepoint5.Start()
            Else
                wrongrangeparameter = wrongrangeparameter + 1
                TextBox2.Text = wrongrangeparameter
                If wrongrangeparameter >= 3 Then
                    sapialarm()
                Else
                    looping = 33
                    TextBox1.Text = looping
                End If
                Timer2.Start()
            End If
        ElseIf looping >= 37 AndAlso looping <= 39 Then
            If DMMtxtparameter.Text.Contains("Vac") AndAlso DMMrange.Text.Contains("6 0") Then
                wrongrangeparameter = 0
                TextBox2.Text = wrongrangeparameter
                SerialPort1.Write("OOUT 50V,45HZX") '50v
                Thread.Sleep(500)
                SerialPort1.Write("OOPERX") 'calibrator operate
                SerialPort1.Write("!")
                If looping = 37 Then
                    Timeronepoint5.Interval = 10000
                Else
                    Timeronepoint5.Interval = 300
                End If
                Timeronepoint5.Start()
            Else
                wrongrangeparameter = wrongrangeparameter + 1
                TextBox2.Text = wrongrangeparameter
                If wrongrangeparameter >= 3 Then
                    sapialarm()
                Else
                    looping = 36
                    TextBox1.Text = looping
                End If
                Timer2.Start()
            End If
        ElseIf looping >= 40 AndAlso looping <= 42 Then
            If DMMtxtparameter.Text.Contains("Vac") AndAlso DMMrange.Text.Contains("6 0") Then
                wrongrangeparameter = 0
                TextBox2.Text = wrongrangeparameter
                SerialPort1.Write("OOUT 50V,1KHZX") '50
                Thread.Sleep(500)
                SerialPort1.Write("OOPERX") 'calibrator operate
                SerialPort1.Write("!")
                If looping = 40 Then
                    Timeronepoint5.Interval = 10000
                Else
                    Timeronepoint5.Interval = 300
                End If
                Timeronepoint5.Start()
            Else
                wrongrangeparameter = wrongrangeparameter + 1
                TextBox2.Text = wrongrangeparameter
                If wrongrangeparameter >= 3 Then
                    sapialarm()
                Else
                    looping = 39
                    TextBox1.Text = looping
                End If
                Timer2.Start()
            End If
        ElseIf looping >= 43 AndAlso looping <= 45 Then
            If DMMtxtparameter.Text.Contains("Vac") AndAlso DMMrange.Text.Contains("6 00") Then
                wrongrangeparameter = 0
                TextBox2.Text = wrongrangeparameter
                SerialPort1.Write("OOUT 600V,45HZX") '6v
                Thread.Sleep(500)
                SerialPort1.Write("OOPERX") 'calibrator operate
                SerialPort1.Write("!")
                If looping = 43 Then
                    Timeronepoint5.Interval = 10000
                Else
                    Timeronepoint5.Interval = 300
                End If
                Timeronepoint5.Start()
            Else
                wrongrangeparameter = wrongrangeparameter + 1
                TextBox2.Text = wrongrangeparameter
                If wrongrangeparameter >= 3 Then
                    sapialarm()
                Else
                    looping = 42
                    TextBox1.Text = looping
                End If
                Timer2.Start()
            End If
        ElseIf looping >= 46 AndAlso looping <= 48 Then
            If DMMtxtparameter.Text.Contains("Vac") AndAlso DMMrange.Text.Contains("6 00") Then
                wrongrangeparameter = 0
                TextBox2.Text = wrongrangeparameter
                SerialPort1.Write("OOUT 600V,1KHZX") '6v
                Thread.Sleep(500)
                SerialPort1.Write("OOPERX") 'calibrator operate
                SerialPort1.Write("!")
                If looping = 46 Then
                    Timeronepoint5.Interval = 10000
                Else
                    Timeronepoint5.Interval = 300
                End If
                Timeronepoint5.Start()
            Else
                wrongrangeparameter = wrongrangeparameter + 1
                TextBox2.Text = wrongrangeparameter
                If wrongrangeparameter >= 3 Then
                    sapialarm()
                Else
                    looping = 45
                    TextBox1.Text = looping
                End If
                Timer2.Start()
            End If
        ElseIf looping >= 49 AndAlso looping <= 51 Then
            If DMMtxtparameter.Text.Contains("mvac") AndAlso DMMrange.Text.Contains("6 00") Then
                wrongrangeparameter = 0
                TextBox2.Text = wrongrangeparameter
                SerialPort1.Write("OOUT 6mV,45HZX")
                Thread.Sleep(500)
                SerialPort1.Write("OOPERX") 'calibrator operate
                SerialPort1.Write("!")
                If looping = 49 Then
                    Timeronepoint5.Interval = 10000
                Else
                    Timeronepoint5.Interval = 300
                End If
                Timeronepoint5.Start()
            Else
                wrongrangeparameter = wrongrangeparameter + 1
                TextBox2.Text = wrongrangeparameter
                If wrongrangeparameter >= 3 Then
                    sapialarm()
                Else
                    looping = 48
                    TextBox1.Text = looping
                End If
                Timer2.Start()
            End If
        ElseIf looping >= 52 AndAlso looping <= 54 Then
            If DMMtxtparameter.Text.Contains("mvac") AndAlso DMMrange.Text.Contains("6 00") Then
                wrongrangeparameter = 0
                TextBox2.Text = wrongrangeparameter
                SerialPort1.Write("OOUT 600mV,1KHZX")
                Thread.Sleep(500)
                SerialPort1.Write("OOPERX") 'calibrator operate
                SerialPort1.Write("!")
                If looping = 52 Then
                    Timeronepoint5.Interval = 10000
                Else
                    Timeronepoint5.Interval = 300
                End If
                Timeronepoint5.Start()
            Else
                wrongrangeparameter = wrongrangeparameter + 1
                TextBox2.Text = wrongrangeparameter
                If wrongrangeparameter >= 3 Then
                    sapialarm()
                Else
                    looping = 51
                    TextBox1.Text = looping
                End If
                Timer2.Start()
            End If
        ElseIf looping >= 55 AndAlso looping <= 57 Then 'resistor na
            If getwireresistance = 1 Then 'kapag 1 kukuha ng resistance ng wire
                SerialPort1.Write("OOUT 0OHMX")
                Thread.Sleep(500)
                SerialPort1.Write("OSTBYX")
                SerialPort1.Write("#")
                If looping = 55 Then
                    Timeronepoint5.Interval = 3000
                Else
                    Timeronepoint5.Interval = 300
                End If
                Timeronepoint5.Start()
            ElseIf getwireresistance = 0 Then
                If DMMrange.Text.Contains("6 00") Then
                    SerialPort1.Write("OOUT 0OHMX")
                    Thread.Sleep(500)
                    SerialPort1.Write("OOPERX") 'calibrator operate
                    SerialPort1.Write("!")
                    Timeronepoint5.Interval = 3000
                    Timeronepoint5.Start()
                Else
                    wrongrangeparameter = wrongrangeparameter + 1
                    TextBox2.Text = wrongrangeparameter
                    If wrongrangeparameter >= 3 Then
                        sapialarm()
                    Else
                        looping = 54
                        TextBox1.Text = looping
                    End If
                    Timer2.Start()
                End If
            End If
        ElseIf looping >= 58 AndAlso looping <= 60 Then
            If DMMrange.Text.Contains("6 00") Then
                SerialPort1.Write("OOUT 500OHMX")
                Thread.Sleep(500)
                SerialPort1.Write("OOPERX") 'calibrator operate
                SerialPort1.Write("!")
                If looping = 58 Then
                    Timeronepoint5.Interval = 3000
                Else
                    Timeronepoint5.Interval = 300
                End If
                Timeronepoint5.Start()
            Else
                wrongrangeparameter = wrongrangeparameter + 1
                TextBox2.Text = wrongrangeparameter
                If wrongrangeparameter >= 3 Then
                    sapialarm()
                Else
                    looping = 57
                    TextBox1.Text = looping
                End If
                Timer2.Start()
            End If
        ElseIf looping >= 61 AndAlso looping <= 63 Then
            If DMMtxtparameter.Text.Contains("k") AndAlso DMMrange.Text.Contains("6") Then
                SerialPort1.Write("OOUT 5KOHMX")
                Thread.Sleep(500)
                SerialPort1.Write("OOPERX") 'calibrator operate
                SerialPort1.Write("!")
                If looping = 61 Then
                    Timeronepoint5.Interval = 3000
                Else
                    Timeronepoint5.Interval = 300
                End If
                Timeronepoint5.Start()
            Else
                wrongrangeparameter = wrongrangeparameter + 1
                TextBox2.Text = wrongrangeparameter
                If wrongrangeparameter >= 3 Then
                    sapialarm()
                Else
                    looping = 60
                    TextBox1.Text = looping
                End If
                Timer2.Start()
            End If
        ElseIf looping >= 64 AndAlso looping <= 66 Then
            If DMMtxtparameter.Text.Contains("k") AndAlso DMMrange.Text.Contains("6 0") Then
                SerialPort1.Write("OOUT 50KOHMX")
                Thread.Sleep(500)
                SerialPort1.Write("OOPERX") 'calibrator operate
                SerialPort1.Write("!")
                If looping = 64 Then
                    Timeronepoint5.Interval = 3000
                Else
                    Timeronepoint5.Interval = 300
                End If
                Timeronepoint5.Start()
            Else
                wrongrangeparameter = wrongrangeparameter + 1
                TextBox2.Text = wrongrangeparameter
                If wrongrangeparameter >= 3 Then
                    sapialarm()
                Else
                    looping = 63
                    TextBox1.Text = looping
                End If
                Timer2.Start()
            End If
        ElseIf looping >= 67 AndAlso looping <= 69 Then
            If DMMtxtparameter.Text.Contains("k") AndAlso DMMrange.Text.Contains("6 00") Then
                SerialPort1.Write("OOUT 500KOHMX")
                Thread.Sleep(500)
                SerialPort1.Write("OOPERX") 'calibrator operate
                SerialPort1.Write("!")
                If looping = 67 Then
                    Timeronepoint5.Interval = 3000
                Else
                    Timeronepoint5.Interval = 300
                End If
                Timeronepoint5.Start()
            Else
                wrongrangeparameter = wrongrangeparameter + 1
                TextBox2.Text = wrongrangeparameter
                If wrongrangeparameter >= 3 Then
                    sapialarm()
                Else
                    looping = 66
                    TextBox1.Text = looping
                End If
                Timer2.Start()
            End If
        ElseIf looping >= 70 AndAlso looping <= 72 Then
            If DMMtxtparameter.Text.Contains("M") AndAlso DMMrange.Text.Contains("6") Then
                SerialPort1.Write("OOUT 5MOHMX")
                Thread.Sleep(500)
                SerialPort1.Write("OOPERX") 'calibrator operate
                SerialPort1.Write("!")
                If looping = 70 Then
                    Timeronepoint5.Interval = 3000
                Else
                    Timeronepoint5.Interval = 300
                End If
                Timeronepoint5.Start()
            Else
                wrongrangeparameter = wrongrangeparameter + 1
                TextBox2.Text = wrongrangeparameter
                If wrongrangeparameter >= 3 Then
                    sapialarm()
                Else
                    looping = 69
                    TextBox1.Text = looping
                End If
                Timer2.Start()
            End If
        ElseIf looping >= 73 AndAlso looping <= 75 Then
            If DMMtxtparameter.Text.Contains("M") AndAlso DMMrange.Text.Contains("6 0") Then
                SerialPort1.Write("OOUT 10MOHMX")
                Thread.Sleep(500)
                SerialPort1.Write("OOPERX") 'calibrator operate
                SerialPort1.Write("!")
                If looping = 73 Then
                    Timeronepoint5.Interval = 3000
                Else
                    Timeronepoint5.Interval = 300
                End If
                Timeronepoint5.Start()
            Else
                wrongrangeparameter = wrongrangeparameter + 1
                TextBox2.Text = wrongrangeparameter
                If wrongrangeparameter >= 3 Then
                    sapialarm()
                Else
                    looping = 72
                    TextBox1.Text = looping
                End If
                Timer2.Start()
            End If
        ElseIf looping >= 76 AndAlso looping <= 78 Then
            If DMMtxtparameter.Text.Contains("M") AndAlso DMMrange.Text.Contains("6 0") Then
                SerialPort1.Write("OOUT 30MOHMX")
                Thread.Sleep(500)
                SerialPort1.Write("OOPERX") 'calibrator operate
                SerialPort1.Write("!")
                If looping = 76 Then
                    Timeronepoint5.Interval = 3000
                Else
                    Timeronepoint5.Interval = 300
                End If
                Timeronepoint5.Start()
            Else
                wrongrangeparameter = wrongrangeparameter + 1
                TextBox2.Text = wrongrangeparameter
                If wrongrangeparameter >= 3 Then
                    sapialarm()
                Else
                    looping = 75
                    TextBox1.Text = looping
                End If
                Timer2.Start()
            End If
        End If

    End Sub

    Private Sub Timeronepoint5_Tick(sender As Object, e As EventArgs) Handles Timeronepoint5.Tick
        Timeronepoint5.Stop()
        checkreading()         ' sets DMMreading.Text ONLY
        If malingreading = 0 Then
            inputReadingToMvTextbox()  ' writes to MV1/MV2/MV3 here
            If looping = 54 And getwireresistance = 0 Then 'huling measure ng voltage kaya mag resistance na
                getwireresistance = 1 'mag measure ng resistance
                looping = looping + 1
            End If
        End If
        If (looping = 3 And malingreading = 0) Or (looping = 6 And malingreading = 0) Or (looping = 9 And malingreading = 0) Or (looping = 12 And malingreading = 0) Or (looping = 15 And malingreading = 0) Or (looping = 18 And malingreading = 0) Or (looping = 21 And malingreading = 0) Or (looping = 24 And malingreading = 0) Or (looping = 27 And malingreading = 0) Or (looping = 30 And malingreading = 0) Or (looping = 33 And malingreading = 0) Or (looping = 36 And malingreading = 0) Or (looping = 39 And malingreading = 0) Or (looping = 42 And malingreading = 0) Or (looping = 45 And malingreading = 0) Or (looping = 48 And malingreading = 0) Or (looping = 51 And malingreading = 0) Or (looping = 54 And malingreading = 0) Or (looping = 57 And malingreading = 0) Or (looping = 60 And malingreading = 0) Or (looping = 63 And malingreading = 0) Or (looping = 66 And malingreading = 0) Or (looping = 69 And malingreading = 0) Or (looping = 72 And malingreading = 0) Or (looping = 75 And malingreading = 0) Or (looping = 78 And malingreading = 0) Then
            SerialPort1.Write("OSTBYX") 'calibrator standby
            SerialPort1.Write("@")
        End If
        Timer2.Start()
    End Sub

    Private Sub Timer2_Tick(sender As Object, e As EventArgs) Handles Timer2.Tick
        Timer2.Stop()
        If malingreading = 0 And getwireresistance = 0 Then
            looping = looping + 1
            TextBox1.Text = looping
        End If
        If looping = 79 Then
            SerialPort1.Write("R225rW4w") 'para pindutin yung range 1 time
            Thread.Sleep(3000) 'delay 3 seconds
            TextBox1.Text = looping
            getwireresistance = 0
        ElseIf looping >= 80 Then
            TextBox1.Text = looping
            SerialPort1.Write("?")
        Else
            BtnCapture.PerformClick()
        End If
    End Sub
#End Region


End Class