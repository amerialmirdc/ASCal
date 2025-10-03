Imports System.Data.OleDb
Imports System.IO
Imports System.Linq

Public Class NEWDMM

    ' one TabControl that lives inside the previewTemplate panel
    Private previewTabs As TabControl

    ' At class scope:
    Private uploadedTemplatePath As String

    Private Sub NEWDMM_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' optional: snap window to working area like the admin form
        Me.StartPosition = FormStartPosition.Manual
        Me.MaximumSize = New Size(0, 0)
        Me.MinimumSize = New Size(0, 0)
        Me.Bounds = Screen.FromControl(Me).WorkingArea

        ' make sure our preview host is ready
        EnsurePreviewHost()
    End Sub

    ' Create/reuse a TabControl inside the existing Designer panel: previewTemplate
    Private Sub EnsurePreviewHost()
        ' previewTemplate exists in NEWDMM.Designer.vb
        Dim host As Panel = Me.previewTemplate
        If host Is Nothing Then
            MessageBox.Show("Panel 'previewTemplate' was not found on this form.", "Preview", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        If previewTabs Is Nothing OrElse previewTabs.IsDisposed Then
            previewTabs = New TabControl With {.Dock = DockStyle.Fill, .Name = "tabPreviewTemplate"}
            host.Controls.Clear()
            host.Controls.Add(previewTabs)
        ElseIf previewTabs.Parent IsNot host Then
            previewTabs.Parent = host
            previewTabs.Dock = DockStyle.Fill
        End If
    End Sub

    ' Show ALL worksheets as separate tables (tabs) inside previewTemplate
    Private Sub ShowTemplatePreviewInline(xlsxPath As String)
        EnsurePreviewHost()
        If previewTabs Is Nothing Then Exit Sub

        previewTabs.TabPages.Clear()

        Dim cs = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={xlsxPath};Extended Properties=""Excel 12.0 Xml;HDR=YES;IMEX=1"""
        Dim tableMap As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

        Using cn As New OleDbConnection(cs)
            cn.Open()

            ' discover all worksheet names
            Dim schema = cn.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, Nothing)
            If schema IsNot Nothing Then
                For Each r As DataRow In schema.Rows
                    Dim raw As String = CStr(r("TABLE_NAME")).Trim()
                    If raw.StartsWith("'") AndAlso raw.EndsWith("'") Then
                        raw = raw.Substring(1, raw.Length - 2)
                    End If
                    If raw.EndsWith("$", StringComparison.Ordinal) OrElse raw.Contains("$") Then
                        Dim norm As String = raw.TrimEnd("$"c)
                        Dim i As Integer = norm.IndexOf("$"c)
                        If i >= 0 Then norm = norm.Substring(0, i)
                        norm = norm.Trim()
                        If Not tableMap.ContainsKey(norm) Then
                            tableMap(norm) = raw   ' keep [$]-suffixed name for SELECT
                        End If
                    End If
                Next
            End If

            ' render each sheet into its own tab with a DataGridView
            For Each sheet In tableMap.Keys.OrderBy(Function(s) s, StringComparer.OrdinalIgnoreCase)
                Dim dt As New DataTable(sheet)
                Using cmd As New OleDbCommand($"SELECT * FROM [{tableMap(sheet)}]", cn)
                    Using adp As New OleDbDataAdapter(cmd)
                        adp.Fill(dt)
                    End Using
                End Using

                Dim tp As New TabPage(sheet)
                Dim dgv As New DataGridView With {
                    .Dock = DockStyle.Fill,
                    .ReadOnly = True,
                    .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
                    .AllowUserToAddRows = False,
                    .AllowUserToDeleteRows = False,
                    .DataSource = dt
                }
                tp.Controls.Add(dgv)
                previewTabs.TabPages.Add(tp)
            Next
        End Using

        If previewTabs.TabPages.Count = 0 Then
            Dim tp As New TabPage("Preview")
            tp.Controls.Add(New Label With {
                .Dock = DockStyle.Fill,
                .TextAlign = ContentAlignment.MiddleCenter,
                .Text = "No worksheets were found in the file."
            })
            previewTabs.TabPages.Add(tp)
        End If
    End Sub

    ' When the user picks a template:
    Private Sub template_Click(sender As Object, e As EventArgs) Handles template.Click
        Using ofd As New OpenFileDialog()
            ofd.Filter = "Excel Files|*.xls;*.xlsx"
            If ofd.ShowDialog() = DialogResult.OK Then
                uploadedTemplatePath = ofd.FileName
                ShowTemplatePreviewInline(ofd.FileName)
            End If
        End Using
    End Sub

    Private Sub newSaveBtn_Click(sender As Object, e As EventArgs) Handles newSaveBtn.Click
        Dim modelText As String = modelNew.Text.Trim()
        Dim manufacturerText As String = manufacturerNew.Text.Trim()
        Dim descriptionText As String = descriptionNew.Text.Trim()

        If String.IsNullOrWhiteSpace(modelText) OrElse String.IsNullOrWhiteSpace(manufacturerText) Then
            MessageBox.Show("Model and Manufacturer fields are required.", "Input Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        Dim existingDmmModels As List(Of String) = LoadAllDMMModels()
        If existingDmmModels.Any(Function(m) m.Equals(modelText, StringComparison.OrdinalIgnoreCase)) Then
            MessageBox.Show("DMM Model already exists. Please check existing entries.", "Conflict",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        ' Build parameters straight from the uploaded template (no ListViews on this form)
        Dim paramDict = BuildParamDictFromExcel(uploadedTemplatePath)

        Try
            SQLiteHelper.InsertOrUpdateDMM("", modelText, manufacturerText, descriptionText, paramDict)

            ' Optional: keep a managed copy of the uploaded Excel (remove if not needed)
            If Not String.IsNullOrEmpty(uploadedTemplatePath) AndAlso File.Exists(uploadedTemplatePath) Then
                Dim templatesRoot As String = Path.Combine(Application.StartupPath, "Templates")
                If Not Directory.Exists(templatesRoot) Then Directory.CreateDirectory(templatesRoot)
                Dim safeName = String.Concat(modelText.Where(Function(c) Not Path.GetInvalidFileNameChars().Contains(c)))
                Dim destPath = Path.Combine(templatesRoot, safeName & Path.GetExtension(uploadedTemplatePath))
                File.Copy(uploadedTemplatePath, destPath, True)
            End If

            ' … after you’ve successfully saved …
            MessageBox.Show("DMM and parameters saved from template.", "Success",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)

            ' open the management screen
            Dim mgmt As New dmmManagementAdmin()
            mgmt.Show()

            ' close or hide the current NEWDMM form
            Me.Close()
        Catch ex As Exception
            MessageBox.Show("Error saving DMM: " & ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' Builds: Category -> RangeLabel -> List of (Nominal, Third)
    ' AC-like sheets (ACV/ACC) use Third = Frequency; others use Third = Unit
    Private Function BuildParamDictFromExcel(filePath As String) _
    As Dictionary(Of String, Dictionary(Of String, List(Of Tuple(Of String, String))))

        Dim result As New Dictionary(Of String, Dictionary(Of String, List(Of Tuple(Of String, String))))()
        If String.IsNullOrWhiteSpace(filePath) OrElse Not File.Exists(filePath) Then Return result

        Dim cs = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={filePath};Extended Properties=""Excel 12.0 Xml;HDR=YES;IMEX=1"""
        Dim tableMap As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

        Using cn As New OleDb.OleDbConnection(cs)
            cn.Open()
            Dim schema = cn.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, Nothing)
            If schema IsNot Nothing Then
                For Each r As DataRow In schema.Rows
                    Dim raw As String = CStr(r("TABLE_NAME")).Trim()
                    If raw.StartsWith("'") AndAlso raw.EndsWith("'") Then raw = raw.Substring(1, raw.Length - 2)
                    If raw.EndsWith("$", StringComparison.Ordinal) OrElse raw.Contains("$") Then
                        Dim norm As String = raw.TrimEnd("$"c)
                        Dim i As Integer = norm.IndexOf("$"c) : If i >= 0 Then norm = norm.Substring(0, i)
                        norm = norm.Trim()
                        If Not tableMap.ContainsKey(norm) Then tableMap(norm) = raw
                    End If
                Next
            End If

            ' Loop through *all* sheets found, treat sheet name as category
            For Each sheetName In tableMap.Keys
                Dim categoryName = sheetName.Trim() ' "ACV","DCmA","DCmV","Continuity", etc.
                If Not result.ContainsKey(categoryName) Then
                    result(categoryName) = New Dictionary(Of String, List(Of Tuple(Of String, String)))()
                End If

                Using cmd As New OleDb.OleDbCommand($"SELECT * FROM [{tableMap(sheetName)}]", cn)
                    Using reader As OleDb.OleDbDataReader = cmd.ExecuteReader()
                        If reader Is Nothing Then Continue For

                        ' header names
                        Dim headers As New List(Of String)
                        For i = 0 To reader.FieldCount - 1
                            headers.Add(reader.GetName(i))
                        Next

                        ' guess which columns to use
                        ' always try RangeLabel/Range, Nominal/Nominal Value, Frequency/Freq/Hz or Unit
                        While reader.Read()
                            Dim rangeLabel = GetColumnValue(reader, headers, "RangeLabel", "Range")
                            Dim nominal = GetColumnValue(reader, headers, "Nominal", "Nominal Value")

                            ' detect third col automatically: Frequency if present else Unit
                            Dim third As String = GetColumnValue(reader, headers, "Frequency", "Freq", "Hz")
                            If String.IsNullOrEmpty(third) Then
                                third = GetColumnValue(reader, headers, "Unit")
                            End If

                            If String.IsNullOrWhiteSpace(rangeLabel) OrElse String.IsNullOrWhiteSpace(nominal) Then Continue While
                            If Not result(categoryName).ContainsKey(rangeLabel) Then
                                result(categoryName)(rangeLabel) = New List(Of Tuple(Of String, String))()
                            End If
                            result(categoryName)(rangeLabel).Add(Tuple.Create(nominal, third))
                        End While
                    End Using
                End Using
            Next
        End Using

        Return result
    End Function

    ' Safe header lookup that accepts multiple possible names
    Private Function GetColumnValue(reader As OleDbDataReader, headers As List(Of String), ParamArray possibleNames() As String) As String
        For Each possibleName As String In possibleNames
            For i As Integer = 0 To headers.Count - 1
                If String.Equals(headers(i), possibleName, StringComparison.OrdinalIgnoreCase) Then
                    If Not reader.IsDBNull(i) Then
                        Return reader(i).ToString().Trim()
                    Else
                        Return String.Empty
                    End If
                End If
            Next
        Next
        Return String.Empty
    End Function

    ' Returns the full path where this model’s template file lives
    Private Function GetPerModelTemplatePath(modelName As String) As String
        ' You can change the folder; this matches what admin uses
        Dim templatesRoot As String = Path.Combine(Application.StartupPath, "Templates")
        If Not Directory.Exists(templatesRoot) Then
            Directory.CreateDirectory(templatesRoot)
        End If

        ' file name based on model
        Dim safeName = String.Concat(modelName.Where(Function(c) Not Path.GetInvalidFileNameChars().Contains(c)))
        Return Path.Combine(templatesRoot, safeName & ".xlsx")
    End Function

    ' Copies the master template into the per-model file
    Private Sub ExportTemplateForModel(modelName As String)
        ' your master template lives alongside the EXE, adjust as needed
        Dim masterTemplatePath As String = Path.Combine(Application.StartupPath, "fluketemplate.xlsx")
        Dim destPath As String = GetPerModelTemplatePath(modelName)

        ' overwrite if already exists
        File.Copy(masterTemplatePath, destPath, True)
    End Sub

    Private Sub backBtn_Click(sender As Object, e As EventArgs) Handles backBtn.Click
        ' open the management screen
        Dim mgmt As New dmmManagementAdmin()
        mgmt.Show()

        ' close or hide the current NEWDMM form
        Me.Close()
    End Sub

End Class