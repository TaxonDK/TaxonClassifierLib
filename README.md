# TaxonClassifierLib

## Description
The TaxonClassifierLib is a library for classifying text written C#. It takes a plain UTF-8 text along with a JSON string lookup taxonomy and some settings (see manual) and returns a JSON string.


## How to use

First step is to convert the original taxonomy to a lookup taxonomy. It is done by calling MakeLookupTaxonomy with the original taxonomy as a JSON string.

>   string lookupJSON = makeLookupTaxonomy(taxonomyJSON);

The resulting JSON string is used when calling classifyText

>   string result = classifyText(text, lookupText, settings);

where settings is a Dictionary<string, string>

>   PowerShell: $settings = [System.Collections.Generic.Dictionary[string,string]]::new()
>   C#: Dictionary<string,string> settings = new Dictionary<string,string>();

## Demo PowerShell script
The TaxonClassiferLib_Demo.ps1 is a PowerShell script for demonstrating the process of classifying a text with a taxonomy. The default taxonomy is the OS2KLE (see https://github.com/os2kle/os2kle).

Download the PowerShell script and place it in an empty folder. The script downloads all needed files and initializes the system. Change the testtext.txt file with your own test text (in Danish).

### The binary TaxonClassfierLib.dll (always be careful with binary files!)
We provide the binary TaxonClassifierLib.dll for testing purposes. It may or may not be up to date.
