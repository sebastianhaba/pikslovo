# Pikslovo

## Project documentation

Project requirements, domain model, and architecture decision records are in `docs/`.
Read the MVP specification and relevant ADRs before changing the product scope or Android
integration architecture.

## Commit messages

Use the Conventional Commits format for every commit message:
`type(optional-scope): concise imperative description`.

Use an appropriate lowercase type, such as `feat`, `fix`, `docs`, `refactor`, `style`,
`test`, `build`, `ci`, or `chore`.

## Reference projects

Use these projects as behavioural and UX references when evolving the translator:

- [Decky-Translator](https://github.com/cat-in-a-box/Decky-Translator): screenshot OCR,
  translation, and a temporary translated-screen overlay.
- [PlayTranslate](https://github.com/dominostars/playtranslate): Android-oriented capture and
  translation workflow. Pikslovo intentionally keeps the first version simpler.

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
dotnet msbuild src/Pikslovo/Pikslovo/Pikslovo.csproj \
  -p:TargetFramework=net10.0-android -p:Configuration=Debug -p:RuntimeIdentifier=android-arm64 \
  -t:Package,_Sign
```

Install the output on the connected device:

```bash
/home/sho/Android/Sdk/platform-tools/adb -s RG477M01025672 install -r --no-incremental \
  src/Pikslovo/Pikslovo/bin/Debug/net10.0-android/android-arm64/app.pikslovo-Signed.apk
```

Keep `EmbedAssembliesIntoApk` enabled for debug packages. The target LineageOS/GammaOS
device does not support .NET for Android Fast Deployment reliably.
