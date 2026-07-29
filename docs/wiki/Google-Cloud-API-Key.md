Pikslovo uses your own Google Cloud API key to recognize and translate text.
The app sends the screenshots you choose to translate to Google Cloud Vision API,
then sends the recognized text to Cloud Translation API. Google bills usage to
your Google Cloud project.

## Before you start

You need a Google account and a Google Cloud billing account. Both Cloud Vision
API and Cloud Translation API require billing to be enabled for the project,
even if your usage stays within any available free allowance.

## 1. Create a Google Cloud project

1. Open the [Google Cloud Console](https://console.cloud.google.com/).
2. At the top of the page, open the project selector and click **New Project**.
3. Enter a project name, then click **Create**.
4. Make sure the new project is selected in the project selector.

Google Cloud may ask you to complete this step while enabling an API. The
project must have billing enabled before Pikslovo can use the required APIs.

## 2. Enable the required APIs

1. Open [APIs & Services > Library](https://console.cloud.google.com/apis/library).
2. Search for **Cloud Vision API**, open it, and click **Enable**.
3. Return to the library, search for **Cloud Translation API**, open it, and
   click **Enable**.

## 3. Create an API key

1. Open [APIs & Services > Credentials](https://console.cloud.google.com/apis/credentials).
2. Click **Create credentials** and choose **API key**.
3. Copy the generated key. You can use **Show key** from the Credentials page
   later if you need to view it again.

## 4. Restrict the key (optional)

Restrict the key to the two APIs that Pikslovo uses:

1. On the **Credentials** page, click the API key you created.
2. Under **API restrictions**, select **Restrict key**.
3. Select **Cloud Vision API** and **Cloud Translation API**.
4. Click **Save**.

## 6. Add the key to Pikslovo

1. Open **Pikslovo** on your device.
2. Open **Settings** and select **Google Cloud API**. During first-run setup,
   you can enter the key on the same **Google Cloud API** step.
3. Paste the key into the **Google Cloud API key** field.
4. Tap **Check key**.

When the check succeeds, Pikslovo confirms that the key can access both Cloud
Translation API and Cloud Vision API. The key is encrypted locally with Android
Keystore and is never included in Pikslovo diagnostics reports.

## Troubleshooting

- **The key cannot access an API:** confirm that both APIs are enabled in the
  same Google Cloud project as the key, then save the API restrictions again.
- **A billing or quota error:** make sure that the project has an active billing
  account and that its quota or budget settings allow usage.
- **Check key fails after restricting it:** verify that the only selected API
  restrictions are **Cloud Vision API** and **Cloud Translation API**.
