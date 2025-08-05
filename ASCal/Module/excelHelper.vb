Imports System.Data.SQLite
Imports ClosedXML.Excel
Imports System.IO

Module excelHelper

    Public Function LoadExcelData(filePath As String) As Dictionary(Of String, String)
        Dim data As New Dictionary(Of String, String)()

        Try
            Using workbook As New XLWorkbook(filePath)
                Dim ws = workbook.Worksheet("DataSheet")

                ' --- Basic Fields ---
                data("workOrderNumber") = ws.Cell("L7").GetValue(Of String)()
                data("technicalID") = ws.Cell("AN7").GetValue(Of String)()
                data("description") = ws.Cell("K9").GetValue(Of String)()
                data("manufacturer") = ws.Cell("K10").GetValue(Of String)()
                data("model") = ws.Cell("K11").GetValue(Of String)()
                data("serialNumber") = ws.Cell("K12").GetValue(Of String)()
                data("range") = ws.Cell("K13").GetValue(Of String)()
                data("readability") = ws.Cell("K14").GetValue(Of String)()
                data("prevCalCert") = ws.Cell("K15").GetValue(Of String)()

                data("receivedDate") = ws.Cell("A9").GetValue(Of String)()
                data("calibrationDate") = ws.Cell("A10").GetValue(Of String)()
                data("optionsInstalled") = ws.Cell("A11").GetValue(Of String)()
                data("customerPO") = ws.Cell("A12").GetValue(Of String)()
                data("assetNumber") = ws.Cell("A13").GetValue(Of String)()
                data("accuracy") = ws.Cell("A14").GetValue(Of String)()
                data("previousTechnician") = ws.Cell("A15").GetValue(Of String)()

                ' --- Company Info ---
                data("companyName") = ws.Cell("AG29").GetValue(Of String)()
                data("companyAddress") = ws.Cell("H27").GetValue(Of String)()

                ' --- Calibration Location ---
                data("isInhouse1") = ws.Cell("AE25").GetValue(Of String)()
                data("isInhouse2") = ws.Cell("AE26").GetValue(Of String)()
                data("onsiteAddress") = ws.Cell("AG29").GetValue(Of String)()

                ' --- Reference Standards Used ---
                data("refDesc1") = ws.Cell("A33").GetValue(Of String)()
                data("refDesc2") = ws.Cell("A34").GetValue(Of String)()
                data("refSerial1") = ws.Cell("Q33").GetValue(Of String)()
                data("refSerial2") = ws.Cell("Q34").GetValue(Of String)()
                data("refCalRef1") = ws.Cell("AB33").GetValue(Of String)()
                data("refCalRef2") = ws.Cell("AB34").GetValue(Of String)()
                data("refDue1") = ws.Cell("AO33").GetValue(Of String)()
                data("refDue2") = ws.Cell("AO34").GetValue(Of String)()

                ' --- Accessories Used ---
                data("accDesc1") = ws.Cell("A37").GetValue(Of String)()
                data("accDesc2") = ws.Cell("A38").GetValue(Of String)()
                data("accSerial1") = ws.Cell("Q37").GetValue(Of String)()
                data("accSerial2") = ws.Cell("Q38").GetValue(Of String)()
                data("accCalRef1") = ws.Cell("AB37").GetValue(Of String)()
                data("accCalRef2") = ws.Cell("AB38").GetValue(Of String)()
                data("accDue1") = ws.Cell("AO37").GetValue(Of String)()
                data("accDue2") = ws.Cell("AO38").GetValue(Of String)()

                ' --- Environmental Conditions ---
                data("tempStart") = ws.Cell("K41").GetValue(Of String)()
                data("tempEnd") = ws.Cell("K42").GetValue(Of String)()
                data("humidityStart") = ws.Cell("T41").GetValue(Of String)()
                data("humidityEnd") = ws.Cell("T42").GetValue(Of String)()

            End Using
        Catch ex As Exception
            MessageBox.Show("Failed to load Excel data: " & ex.Message, "Excel Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try

        Return data
    End Function

    Public Function LoadExcelValuesForCalibration(filePath As String) As Dictionary(Of String, String)
        Dim data As New Dictionary(Of String, String)()

        Try
            Using workbook As New XLWorkbook(filePath)
                Dim ws = workbook.Worksheet("DataSheet")

                ' --- Basic Fields ---
                data("workOrderNumber") = ws.Cell("L7").GetValue(Of String)()
                data("technicalID") = ws.Cell("AN7").GetValue(Of String)()

                data("description") = ws.Cell("K9").GetValue(Of String)()
                data("manufacturer") = ws.Cell("K10").GetValue(Of String)()
                data("model") = ws.Cell("K11").GetValue(Of String)()
                data("serialNumber") = ws.Cell("K12").GetValue(Of String)()
                data("range") = ws.Cell("K13").GetValue(Of String)()
                data("readability") = ws.Cell("K14").GetValue(Of String)()
                data("prevCalCert") = ws.Cell("K15").GetValue(Of String)()

                data("receivedDate") = ws.Cell("A9").GetValue(Of String)()
                data("calibrationDate") = ws.Cell("A10").GetValue(Of String)()
                data("optionsInstalled") = ws.Cell("A11").GetValue(Of String)()
                data("customerPO") = ws.Cell("A12").GetValue(Of String)()
                data("assetNumber") = ws.Cell("A13").GetValue(Of String)()
                data("accuracy") = ws.Cell("A14").GetValue(Of String)()
                data("previousTechnician") = ws.Cell("A15").GetValue(Of String)()

                ' --- Company Info ---
                data("companyName") = ws.Cell("AG29").GetValue(Of String)()
                data("companyAddress") = ws.Cell("H27").GetValue(Of String)()

                ' --- Calibration Location ---
                data("isInhouse1") = ws.Cell("AE25").GetValue(Of String)()
                data("isInhouse2") = ws.Cell("AE26").GetValue(Of String)()
                data("onsiteAddress") = ws.Cell("AG29").GetValue(Of String)()

                ' --- Reference Standards Used ---
                data("refDesc1") = ws.Cell("A33").GetValue(Of String)()
                data("refDesc2") = ws.Cell("A34").GetValue(Of String)()
                data("refSerial1") = ws.Cell("Q33").GetValue(Of String)()
                data("refSerial2") = ws.Cell("Q34").GetValue(Of String)()
                data("refCalRef1") = ws.Cell("AB33").GetValue(Of String)()
                data("refCalRef2") = ws.Cell("AB34").GetValue(Of String)()
                data("refDue1") = ws.Cell("AO33").GetValue(Of String)()
                data("refDue2") = ws.Cell("AO34").GetValue(Of String)()

                ' --- Accessories Used ---
                data("accDesc1") = ws.Cell("A37").GetValue(Of String)()
                data("accDesc2") = ws.Cell("A38").GetValue(Of String)()
                data("accSerial1") = ws.Cell("Q37").GetValue(Of String)()
                data("accSerial2") = ws.Cell("Q38").GetValue(Of String)()
                data("accCalRef1") = ws.Cell("AB37").GetValue(Of String)()
                data("accCalRef2") = ws.Cell("AB38").GetValue(Of String)()
                data("accDue1") = ws.Cell("AO37").GetValue(Of String)()
                data("accDue2") = ws.Cell("AO38").GetValue(Of String)()

                ' --- Environmental Conditions ---
                data("tempStart") = ws.Cell("K41").GetValue(Of String)()
                data("tempEnd") = ws.Cell("K42").GetValue(Of String)()
                data("humidityStart") = ws.Cell("T41").GetValue(Of String)()
                data("humidityEnd") = ws.Cell("T42").GetValue(Of String)()
            End Using
        Catch ex As Exception
            MessageBox.Show("Failed to load Excel data: " & ex.Message, "Excel Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try

        Return data
    End Function

    Public Sub SaveCalibrationToExcel(templatePath As String, savePath As String, data As Dictionary(Of String, String))
        Try
            File.Copy(templatePath, savePath, True)

            Using workbook As New XLWorkbook(savePath)
                Dim ws = workbook.Worksheet("DataSheet")

                ' --- Write Basic Info ---
                ws.Cell("L7").Value = data("workOrderNumber")
                ws.Cell("AN7").Value = data("technicalID")
                ws.Cell("K9").Value = data("description")
                ws.Cell("K10").Value = data("manufacturer")
                ws.Cell("K11").Value = data("model")
                ws.Cell("K12").Value = data("serialNumber")
                ws.Cell("K13").Value = data("range")
                ws.Cell("K14").Value = data("readability")
                ws.Cell("K15").Value = data("prevCalCert")

                ws.Cell("A9").Value = data("receivedDate")
                ws.Cell("A10").Value = data("calibrationDate")
                ws.Cell("A11").Value = data("optionsInstalled")
                ws.Cell("A12").Value = data("customerPO")
                ws.Cell("A13").Value = data("assetNumber")
                ws.Cell("A14").Value = data("accuracy")
                ws.Cell("A15").Value = data("previousTechnician")

                ' --- Company ---
                ws.Cell("AG29").Value = data("companyName")
                ws.Cell("H27").Value = data("companyAddress")

                ' --- In-house or On-site ---
                ws.Cell("AE25").Value = data("isInhouse1")
                ws.Cell("AE26").Value = data("isInhouse2")

                ' --- Reference Standards ---
                ws.Cell("A33").Value = data("refDesc1")
                ws.Cell("A34").Value = data("refDesc2")
                ws.Cell("Q33").Value = data("refSerial1")
                ws.Cell("Q34").Value = data("refSerial2")
                ws.Cell("AB33").Value = data("refCalRef1")
                ws.Cell("AB34").Value = data("refCalRef2")
                ws.Cell("AO33").Value = data("refDue1")
                ws.Cell("AO34").Value = data("refDue2")

                ' --- Accessories Used ---
                ws.Cell("A37").Value = data("accDesc1")
                ws.Cell("A38").Value = data("accDesc2")
                ws.Cell("Q37").Value = data("accSerial1")
                ws.Cell("Q38").Value = data("accSerial2")
                ws.Cell("AB37").Value = data("accCalRef1")
                ws.Cell("AB38").Value = data("accCalRef2")
                ws.Cell("AO37").Value = data("accDue1")
                ws.Cell("AO38").Value = data("accDue2")

                ' --- Environmental Conditions ---
                ws.Cell("K41").Value = data("tempStart")
                ws.Cell("K42").Value = data("tempEnd")
                ws.Cell("T41").Value = data("humidityStart")
                ws.Cell("T42").Value = data("humidityEnd")

                workbook.Save()
            End Using

            MessageBox.Show("Calibration Excel file saved to: " & savePath, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show("Error saving calibration to Excel: " & ex.Message, "Excel Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

End Module