# Development
Copy-Item -Path "C:\Users\Brian\source\repos\TaxonClassifierLib\TaxonClassifierLib\bin\Debug\net8.0\TaxonClassifierLib.dll" -Destination ".\"

$currentFolder = (Get-Location).Path

$dllFile = "$currentFolder\TaxonClassifierLib.dll"
$os2kleFile = "$currentFolder\OS2KLE.json"

$text = "hul hul hul vej vej rotte"

$taxonClassifier = [System.Reflection.Assembly]::LoadFrom($dllFile)

$types = $taxonClassifier.GetTypes()
$classify = $types[0].GetMethod("classifyText")
$makeLookup = $types[1].GetMethod("makeLookupTaxonomy")

$os2kleJSON = Get-Content $os2kleFile -Raw

$os2kle_lookupJSON = $makeLookup.Invoke($null, @($os2kleJSON.ToString()))

$settings = [System.Collections.Generic.Dictionary[string,string]]::new()

$result = $classify.Invoke($null, @($text, $os2kle_lookupJSON, $settings))

Write-Output $result.ToString()

