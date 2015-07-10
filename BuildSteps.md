#Building and Publishing
A document to remind myself of the steps for building and publishing a new version

## Build Steps
This step builds the Release configuration and performs a web site publish to the artifacts folder

1. Set version in EventHubPeek\Properties\AssemblyInfo.cs
2. Run build.cmd from the "MSBuild Command Prompt"

## Package Stes
This step packages the artifacts folder into the nupkg

1. Set version in EventHubPeek.nuspec
2. Run pack.cmd

## Publish step
This step publishes the package to siteextensions.net. 
Once published it will appear in the site extension list in the Azure portal (it can take a few minutes to show up)

1. Navigate to https://www.siteextensions.net/packages/upload and upload the nupkg

