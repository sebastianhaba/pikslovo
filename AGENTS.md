# GameTranslator

## Project documentation

Project requirements, domain model, and architecture decision records are in `docs/`.
Read the MVP specification and relevant ADRs before changing the product scope or Android
integration architecture.

## Local Android build environment

- Android SDK: `/home/sho/Android/Sdk`
- JDK: `/home/sho/.jdks/ms-21.0.11` (Microsoft OpenJDK 21)
- Target device ABI: `arm64-v8a`

Set the build environment before invoking `dotnet`:

```bash
export ANDROID_HOME=/home/sho/Android/Sdk
export ANDROID_SDK_ROOT=/home/sho/Android/Sdk
export JAVA_HOME=/home/sho/.jdks/ms-21.0.11
export PATH="$JAVA_HOME/bin:$PATH"
export TMPDIR="$PWD/.tmp"
export DOTNET_CLI_HOME="$PWD/.dotnet"
```

Build and sign the installable debug APK:

```bash
dotnet build src/GameTranslator/GameTranslator/GameTranslator.csproj \
  -f net10.0-android -c Debug -r android-arm64 --no-restore -t:SignAndroidPackage
```

Install the output on the connected device:

```bash
/home/sho/Android/Sdk/platform-tools/adb -s RG477M01025672 install -r \
  src/GameTranslator/GameTranslator/bin/Debug/net10.0-android/android-arm64/com.gametranslator-Signed.apk
```

Keep `EmbedAssembliesIntoApk` enabled for debug packages. The target LineageOS/GammaOS
device does not support .NET for Android Fast Deployment reliably.
