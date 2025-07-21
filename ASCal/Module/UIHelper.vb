Imports System.Drawing.Drawing2D
Imports System.Drawing.Text
Imports System.IO ' For easy font lookup
Imports System.Reflection

Public Module UIHelper

    ' 🧩 Tawagin ito sa Form_Load o kapag nag-resize ka ng panel
    Public Sub ApplyRoundedBorder(panel As Panel, radius As Integer, borderColor As Color, borderThickness As Integer)
        ' ⚠️ I-disable ang default border para walang conflict
        panel.BorderStyle = BorderStyle.None

        ' 🖌️ Gamitin ang custom Paint handler para sa rounded + colored border
        AddHandler panel.Paint, Sub(sender As Object, e As PaintEventArgs)
                                    ' ✨ Smooth na graphics rendering
                                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias

                                    ' 📏 I-adjust ang rectangle para hindi lumampas ang stroke sa gilid
                                    Dim halfThickness As Integer = borderThickness \ 2
                                    Dim rect As New Rectangle(
                                        halfThickness,
                                        halfThickness,
                                        panel.Width - borderThickness,
                                        panel.Height - borderThickness
                                    )

                                    ' 🌀 Gawa ng rounded path na may fill + border
                                    Using path As GraphicsPath = GetRoundedPath(rect, radius)
                                        ' 🎨 I-fill ang background ng panel
                                        Using brush As New SolidBrush(panel.BackColor)
                                            e.Graphics.FillPath(brush, path)
                                        End Using

                                        ' 🟦 I-draw ang border gamit ang desired color at thickness
                                        Using pen As New Pen(borderColor, borderThickness)
                                            pen.Alignment = PenAlignment.Inset ' 🔐 Siguradong hindi lalampas sa bounds
                                            e.Graphics.DrawPath(pen, path)
                                        End Using
                                    End Using
                                End Sub

        ' ✂️ I-set ang visible region para hindi square ang corners
        panel.Region = New Region(GetRoundedPath(panel.ClientRectangle, radius))

        ' 🔃 Force redraw
        panel.Invalidate()
    End Sub

    ' 📐 Utility function na nagbibigay ng rounded rectangle path
    Private Function GetRoundedPath(rect As Rectangle, radius As Integer) As GraphicsPath
        Dim path As New GraphicsPath()
        Dim d As Integer = radius * 2 ' 🔁 Diameter ng arc

        path.StartFigure()
        path.AddArc(rect.Left, rect.Top, d, d, 180, 90) ' ↖️ Top-left corner
        path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90) ' ↗️ Top-right
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90) ' ↘️ Bottom-right
        path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90) ' ↙️ Bottom-left
        path.CloseFigure() ' 🔒 Close path

        Return path
    End Function

    ' 🟪 Para sa ibang control bukod sa Panel, pwedeng i-clip sa rounded rectangle
    Public Sub ClipControlToRoundedRectangle(control As Control, radius As Integer)
        Dim path As New Drawing2D.GraphicsPath()
        Dim rect = control.ClientRectangle
        Dim diameter = radius * 2

        ' 🌀 Add arcs to simulate rounded rectangle
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90)
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90)
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90)
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90)
        path.CloseFigure()

        ' ✂️ Apply clipping mask
        control.Region = New Region(path)
    End Sub

    ' 🔠 I-apply sa buong form para lahat ng TextBox ay uppercase habang nagta-type
    ' ✅ Pwede rin maglagay ng list ng controls na hindi isasama
    Public Sub ForceUppercaseInput(ByVal form As Form, Optional excludeControls As List(Of String) = Nothing)
        For Each ctrl As Control In form.Controls
            ' 🧠 Lagyan ng KeyPress handler kung TextBox at hindi excluded
            If TypeOf ctrl Is TextBox AndAlso (excludeControls Is Nothing OrElse Not excludeControls.Contains(ctrl.Name)) Then
                AddHandler CType(ctrl, TextBox).KeyPress, AddressOf HandleUppercaseKeyPress
            ElseIf ctrl.HasChildren Then
                ' 🔁 Recursive check para sa nested controls
                ForceUppercaseInputRecursive(ctrl, excludeControls)
            End If
        Next
    End Sub

    ' 🔁 Gawin din recursively sa mga nested controls (e.g. Panel, GroupBox)
    Private Sub ForceUppercaseInputRecursive(ByVal parent As Control, Optional excludeControls As List(Of String) = Nothing)
        For Each ctrl As Control In parent.Controls
            If TypeOf ctrl Is TextBox AndAlso (excludeControls Is Nothing OrElse Not excludeControls.Contains(ctrl.Name)) Then
                AddHandler CType(ctrl, TextBox).KeyPress, AddressOf HandleUppercaseKeyPress
            ElseIf ctrl.HasChildren Then
                ForceUppercaseInputRecursive(ctrl, excludeControls)
            End If
        Next
    End Sub

    ' 🎯 Ito ang actual na event handler para gawing uppercase bawat letter habang nagta-type
    Private Sub HandleUppercaseKeyPress(ByVal sender As Object, ByVal e As KeyPressEventArgs)
        If Char.IsLetter(e.KeyChar) Then
            Dim tb As TextBox = TryCast(sender, TextBox)
            If tb IsNot Nothing Then
                ' 🎯 Insert uppercase version sa tamang lugar sa text
                Dim selStart = tb.SelectionStart
                tb.Text = tb.Text.Insert(selStart, Char.ToUpper(e.KeyChar))
                tb.SelectionStart = selStart + 1 ' 👉 Move cursor forward
                e.Handled = True ' ❌ Huwag na iproseso ang original character
            End If
        End If
    End Sub

    '-------------------------------------- CHECK AND INSTALL FONTS --------------------------------------'

    ' 🧠 Static flag to make sure we only check once per session
    Private fontCheckPerformed As Boolean = False

    Public Sub CheckFontsAcrossAllForms()
        Dim fontsUsed As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        ' 1️⃣ Get current assembly
        Dim asm = Assembly.GetExecutingAssembly()

        ' 2️⃣ Look for all types that are Forms
        For Each t As Type In asm.GetTypes()
            If t.IsSubclassOf(GetType(Form)) AndAlso Not t.IsAbstract Then
                Try
                    ' 3️⃣ Create instance of the form without showing it
                    Dim formInstance As Form = CType(Activator.CreateInstance(t), Form)

                    ' 4️⃣ Collect fonts from controls
                    CollectFontsRecursive(formInstance, fontsUsed)

                    ' Optional: dispose after use to free memory
                    formInstance.Dispose()
                Catch ex As Exception
                    ' 🔒 Handle forms that fail to instantiate
                    Debug.WriteLine(String.Format("Skipping {0}: {1}", t.Name, ex.Message))
                End Try
            End If
        Next

        ' 5️⃣ Check installed fonts
        CheckFontsInstalled(fontsUsed)
    End Sub

    Public Sub CheckFontsInstalled(fontsUsed As HashSet(Of String))
        Dim installed = New InstalledFontCollection()
        ' ✅ Compatible workaround for .NET Framework used by VS2010
        Dim installedNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        For Each f In installed.Families
            installedNames.Add(f.Name)
        Next

        Dim missing = fontsUsed.Where(Function(f) Not installedNames.Contains(f)).ToList()

        If missing.Any() Then
            MessageBox.Show("❌ Missing fonts: " & String.Join(", ", missing), "Missing Fonts", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Application.Exit()
        Else
            MessageBox.Show("✅ All required fonts are installed!", "Fonts OK", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End If
    End Sub

    Public Sub CheckFontsAndNotify(container As Control)
        Dim usedFonts As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        CollectFontsRecursive(container, usedFonts)

        Dim installedFonts As New InstalledFontCollection()
        Dim installedNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each f In installedFonts.Families
            installedNames.Add(f.Name)
        Next

        Dim fontFolderPath = Path.Combine(Application.StartupPath, "Fonts")
        Dim fontFiles As String() = {}
        If Directory.Exists(fontFolderPath) Then
            fontFiles = Directory.GetFiles(fontFolderPath, "*.ttf", SearchOption.AllDirectories)
        End If

        Dim missingFonts As New List(Of String)

        For Each fontName In usedFonts
            If Not installedNames.Contains(fontName) Then
                ' ✅ Attempt to find the font file with a matching internal font name
                Dim matchedFile As String = Nothing
                For Each fontFile In fontFiles
                    Try
                        Using pfc As New PrivateFontCollection()
                            pfc.AddFontFile(fontFile)
                            If pfc.Families.Length > 0 AndAlso pfc.Families(0).Name.Equals(fontName, StringComparison.OrdinalIgnoreCase) Then
                                matchedFile = fontFile
                                Exit For
                            End If
                        End Using
                    Catch ex As Exception
                        ' Skip invalid font files
                    End Try
                Next

                If matchedFile IsNot Nothing Then
                    Try
                        Dim fontsPath = Environment.GetFolderPath(Environment.SpecialFolder.Fonts)
                        Dim targetPath = Path.Combine(fontsPath, Path.GetFileName(matchedFile))

                        If Not File.Exists(matchedFile) Then
                            ' Try extracting from embedded
                            ExtractFontFromEmbeddedResource("YourProjectNamespace.Fonts.YourFont.ttf", fontFolderPath)
                        End If
                    Catch ex As Exception
                        MessageBox.Show("❌ Failed to install font: " & fontName & vbCrLf & ex.Message)
                    End Try
                Else
                    missingFonts.Add(fontName)
                End If
            End If
        Next

        If missingFonts.Count > 0 Then
            MessageBox.Show("❌ Still missing fonts: " & String.Join(", ", missingFonts) &
                            vbCrLf & "Please ensure they exist under: " & fontFolderPath,
                            "Font Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Application.Exit()
        Else
            MessageBox.Show("✅ All required fonts are installed and verified!" & vbCrLf &
                            "Fonts used in this form:" & vbCrLf &
                            String.Join(vbCrLf, usedFonts.OrderBy(Function(f) f)),
                            "Fonts OK", MessageBoxButtons.OK, MessageBoxIcon.Information)

        End If
    End Sub

    Private Sub CollectFontsRecursive(ctrl As Control, fontSet As HashSet(Of String))
        Try
            fontSet.Add(ctrl.Font.Name)
        Catch ex As Exception
            ' Skip if error in getting font
        End Try

        For Each child As Control In ctrl.Controls
            CollectFontsRecursive(child, fontSet)
        Next
    End Sub

    ' 🧰 Extracts .ttf font from embedded resources and saves it to Fonts folder
    Public Sub ExtractFontFromEmbeddedResource(resourceName As String, outputFolder As String)
        Try
            ' 🔍 Find and open embedded font stream
            Dim asm = Reflection.Assembly.GetExecutingAssembly()
            Using fontStream = asm.GetManifestResourceStream(resourceName)
                If fontStream Is Nothing Then
                    MessageBox.Show("❌ Embedded font not found: " & resourceName, "Missing Resource", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    Exit Sub
                End If

                ' 📁 Ensure output folder exists
                If Not Directory.Exists(outputFolder) Then
                    Directory.CreateDirectory(outputFolder)
                End If

                ' 💾 Save .ttf file to output folder
                Dim outputPath = Path.Combine(outputFolder, Path.GetFileName(resourceName))
                Using fs As New FileStream(outputPath, FileMode.Create, FileAccess.Write)
                    fontStream.CopyTo(fs)
                End Using
            End Using
        Catch ex As Exception
            MessageBox.Show("❌ Error extracting embedded font: " & ex.Message, "Font Extraction Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

End Module