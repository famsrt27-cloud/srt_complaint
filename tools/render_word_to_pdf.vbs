Option Explicit

Dim inputPath, outputPath, wordApp, document
If WScript.Arguments.Count <> 2 Then
  WScript.Echo "Usage: cscript render_word_to_pdf.vbs input.docx output.pdf"
  WScript.Quit 2
End If

inputPath = CreateObject("Scripting.FileSystemObject").GetAbsolutePathName(WScript.Arguments(0))
outputPath = CreateObject("Scripting.FileSystemObject").GetAbsolutePathName(WScript.Arguments(1))

On Error Resume Next
Set wordApp = CreateObject("Word.Application")
If Err.Number <> 0 Then
  WScript.Echo "Unable to start Word: " & Err.Description
  WScript.Quit 3
End If

wordApp.Visible = False
wordApp.DisplayAlerts = 0
Set document = wordApp.Documents.Open(inputPath, False, True)
If Err.Number <> 0 Then
  WScript.Echo "Unable to open DOCX: " & Err.Description
  wordApp.Quit
  WScript.Quit 4
End If

document.ExportAsFixedFormat outputPath, 17
If Err.Number <> 0 Then
  WScript.Echo "Unable to export PDF: " & Err.Description
  document.Close False
  wordApp.Quit
  WScript.Quit 5
End If

document.Close False
wordApp.Quit
Set document = Nothing
Set wordApp = Nothing
WScript.Echo outputPath
WScript.Quit 0
