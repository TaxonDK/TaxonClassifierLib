# dllFile needs to be the full path to the .dll
$dllFile = "C:\Users\Brian\source\repos\TaxonClassifierLib\TaxonClassifierLib\bin\Debug\net8.0\TaxonClassifierLib.dll"
$taxonomyFile = "C:\Users\Brian\Projects\Open_Source_TaxonClassifier\OS2KLE.json"
$taxonomy_lookupFile = "C:\Users\Brian\Projects\Open_Source_TaxonClassifier\OS2KLE_lookup.json"

$text = "hul hul hul vej vej rotte"

$taxonClassifier = [System.Reflection.Assembly]::LoadFrom($dllFile)

$types = $taxonClassifier.GetTypes()

$classify = $types[0].GetMethod("classifyText")
$makeLookup = $types[1].GetMethod("makeLookupTaxonomy")

if(Test-Path -Path $taxonomy_lookupFile)
{
	Write-Output "Using lookup taxonomy $taxonomy_lookupFile"

	$taxonomy_lookupJSON = Get-Content $taxonomy_lookupFile -Raw
}
else
{
	Write-Output "Making and saving the lookup taxonomy"

	if(-not(Test-Path -Path $taxonomyFile))
	{
		Write-Output "Downloading OS2KLE from Github"
		
		$url = "https://github.com/os2kle/os2kle/raw/refs/heads/master/OS2KLE.json"
		
		Invoke-WebRequest -Uri $url -OutFile $File
	}

	$taxonomyJSON = Get-Content $taxonomyFile -Raw

	$taxonomy_lookupJSON = $makeLookup.Invoke($null, @($taxonomyJSON.ToString()))

	Set-Content -Path $taxonomy_lookupFile -Value $taxonomy_lookupJSON
}

$settings = [System.Collections.Generic.Dictionary[string,string]]::new()

$result = $classify.Invoke($null, @($text, $taxonomy_lookupJSON.ToString(), $settings))

Write-Output $result.ToString()

