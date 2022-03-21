# GGoogleDriveToDrive

A simple example of a program to download and export files from Google Drive.

# Attention!

To use authorization for the **Google Drive API**, client secrets embedded in the code are used as standard. You can try to use this data or use your own. Alternatively, you can put the file "client_secrets.json" in the directory with the executable file. This data can be taken from Google Cloud Platform (https://console.cloud.google.com/), using OAuth 2.0 Client ID. You can also see an example of this file: [client_secrets.example.json](https://github.com/DrGennadius/GGoogleDriveToDrive/blob/master/GGoogleDriveToDrive/client_secrets.example.json).


# Application configuration


The application configuration is presented in the file 'app_config.json'.


```json
{
  "DownloadsFolder": "Downloads",
  "ContentPullMode": "All",
  "MimeTypesConvertMap": {
    "application/vnd.google-apps.document": {
      "MimeType": "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
      "FileExtension": "docx"
    },
    "application/vnd.google-apps.spreadsheet": {
      "MimeType": "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
      "FileExtension": "xlsx"
    },
    "application/vnd.google-apps.presentation": {
      "MimeType": "application/vnd.openxmlformats-officedocument.presentationml.presentation",
      "FileExtension": "ppsx"
    }
  }
}
```


## Configuration fields description


| Field | Description |
| --- | --- |
| DownloadsFolder | Directory for download content. |
| ContentPullMode | Defines the scope of the loaded content. Possible options: All, IAmOwnerOnly, MyDriveOnly. |
| MimeTypesConvertMap | Used to convert types from Google to another when exporting. Google documents and corresponding export MIME types see [here](https://developers.google.com/drive/api/v3/ref-export-formats?hl=en). |


# Run in commandline


(Optional) You can specify a folder to download the content.

```cmd
.\GGoogleDriveToDrive.exe "D:\GoogleDownloads"
```
