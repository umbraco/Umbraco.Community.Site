# Deploy Zip Files

The zip files in this folder contain content and media for you to import so that you have decent test content for local development. 

Instructions on how to import a zip file, as well as guidance on how to create a zip file (for when you want to share content you've created), are at the bottom of this page.

## Available Files to Import

### 20250715-test-home-page.zip

- Creates a media item in a "Testing" folder
- Creates a home page (using the Test Home doc type & template) with this image
- Publishes the test home page

## How to Import a Zip File

- In the backoffice Content section, right click on the top left "Content" heading and select 'Import'
- Select one of the Zip files in this folder

Umbraco Deploy will then import the contents, assuming there are no schema mismatches. If there are mismatches, check you have pulled to the latest version.

## How to Create a Zip File

- In the content section of your backoffice, right click on the node that you want to share, and select Export. 
   - Tick only "Include content dependencies" and "Include content file dependencies"
- Download the generated zip file and save it to this folder
- Rename the zip file following the existing convention
- Add its name and a list of its contents to this readme file above.

When sharing content and/or media items, please ensure sure that you have also committed all your changes - such as schema (.uda) files, templates (.cshtml), css and other files.