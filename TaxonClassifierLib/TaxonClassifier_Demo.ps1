# dllFile needs to be the full path to the .dll
$dllFile = "C:\Users\Brian\source\repos\TaxonClassifierLib\TaxonClassifierLib\bin\Debug\net8.0\TaxonClassifierLib.dll"
$os2kleFile = ".\OS2KLE.json"
$os2kle_lookupFile = ".\OS2KLE_lookup.json"

$text = "hul hul hul vej vej rotte"

$taxonClassifier = [System.Reflection.Assembly]::LoadFrom($dllFile)

$types = $taxonClassifier.GetTypes()

$classify = $types[0].GetMethod("classifyText")
$makeLookup = $types[1].GetMethod("makeLookupTaxonomy")

if(Test-Path -Path $os2kle_lookupFile)
{
	Write-Output "Using lookup taxonomy $os2kle_lookupFile"

	$os2kle_lookupJSON = Get-Content $os2kle_lookupFile -Raw
}
else
{
	Write-Output "Making and saving the lookup taxonomy"

	if(-not(Test-Path -Path $os2kleFile))
	{
		Write-Output "Downloading OS2KLE from Github"
		
		$url = "https://github.com/os2kle/os2kle/raw/refs/heads/master/OS2KLE.json"
		
		Invoke-WebRequest -Uri $url -OutFile $os2kleFile	
	}

	$os2kleJSON = Get-Content $os2kleFile -Raw

	$os2kle_lookupJSON = $makeLookup.Invoke($null, @($os2kleJSON.ToString()))

	Set-Content -Path $os2kle_lookupFile -Value $os2kle_lookupJSON
}

$settings = [System.Collections.Generic.Dictionary[string,string]]::new()

$result = $classify.Invoke($null, @($text, $os2kle_lookupJSON.ToString(), $settings))

Write-Output $result.ToString()

