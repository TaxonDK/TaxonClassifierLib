# dllFile needs to be the full path to the .dll
$folder = (Get-Location).Path
$dllFile = "$folder\TaxonClassifierLib.dll"
$taxonomyFile = ".\OS2KLE.json"
$taxonomy_lookupFile = ".\OS2KLE_lookup.json"
$textFile = ".\testtext.txt"

# Check for all the files and download those missing
if(-not(Test-Path -Path $textFile))
{
	Write-Output "Downloading sample text file from Github"
	
	$url = "https://raw.githubusercontent.com/TaxonDK/TaxonClassifierLib/refs/heads/master/TaxonClassifierLib/testtext.txt?token=GHSAT0AAAAAADE3PIQAKYKSW5OKYHYCNPMO2B27ULQ"
	
	Invoke-WebRequest -Uri $url -OutFile $textFile
}

$text = Get-Content $textFile -Raw

if(-not(Test-Path -Path $dllFile))
{
	Write-Output "Downloading TaxonClassifierLib.dll from Github"
	
	$url = "https://github.com/TaxonDK/TaxonClassifierLib/raw/refs/heads/master/TaxonClassifierLib/TaxonClassifierLib.dll"
	
	Invoke-WebRequest -Uri $url -OutFile $dllFile
}

try
{
	$taxonClassifier = [System.Reflection.Assembly]::LoadFrom($dllFile)

	$types = $taxonClassifier.GetTypes()
}
catch
{
	Write-Output "Failed loading .dll."
	Write-Output "You probably need some sort of admin execution privileges. Try using the POwerShell prompt."
	Write-Output "Otherwise consult your IT department."
	Write-Output " "

	Read-Host "Press Enter to close script"	
	
	exit
}

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
		
		Invoke-WebRequest -Uri $url -OutFile $taxonomyFile
	}

	$taxonomyJSON = Get-Content $taxonomyFile -Raw

	$taxonomy_lookupJSON = $makeLookup.Invoke($null, @($taxonomyJSON.ToString()))

	Set-Content -Path $taxonomy_lookupFile -Value $taxonomy_lookupJSON
}

$settings = [System.Collections.Generic.Dictionary[string,string]]::new()

$result = $classify.Invoke($null, @($text.ToString(), $taxonomy_lookupJSON.ToString(), $settings))

Write-Output $result.ToString()

Read-Host "Press Enter to close script"