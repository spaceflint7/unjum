# Unjum

A simple puzzle game for Android, written in F# 5 (.NET 5).  It serves as a proof-of-concept application for [Bluebonnet](https://github.com/spaceflint7/bluebonnet) and [BNA](https://github.com/spaceflint7/bna).

**Bluebonnet** is a light-weight .NET platform for Android Java which translates compiled .NET assemblies into Java classes.  **BNA** is an Android-compatible implementation of XNA 4, written in C# and translated to Android Java using Bluebonnet.

This game was developed on Windows against XNA, and the resulting binary (i.e., the .NET DLL which is executable on Windows) was translated through Bluebonnet, combined with BNA and packaged into an Android APK.  Check out the build script [``buildapk.bat``](https://github.com/spaceflint7/unjum/blob/master/Android/buildapk.bat) and the dependent MSBuild project [``MakeAPK.project``](https://github.com/spaceflint7/bna/blob/master/MakeAPK.project) (in BNA).

## Building

- Install XNA 4 on Windows.  See [Installing XNA on Visual Studio 2019](https://ridilabs.net/post/2019/09/20/Installing-XNA-on-Visual-Studio-2019.aspx) and [Visual Studio 2019 XNA Setup](https://flatredball.com/visual-studio-2019-xna-setup/).

- In Visual Studio (version 16.8 or later, for .NET 5 support), open the ``Play.sln`` solution, and build the ``ContentBuilder`` and then the ``Play`` projects.

- Binaries for Bluebonnet and BNA binaries are availabe as part of [Bluebonnet release 0.11](https://github.com/spaceflint7/bluebonnet/releases/tag/v0.11).

- Fix the paths in the build batch file [``buildapk.bat``](https://github.com/spaceflint7/unjum/blob/master/Android/buildapk.bat) to point to the downloaded binaries, as well as your Android directories.

- Create a keystore in ``Android\my.keystore`` using the Java ``signtool`` program:

        "%JAVA_HOME%\bin\keytool.exe" -genkey -v -keystore Android\my.keystore -alias MyAlias -keyalg RSA -keysize 2048 -validity 10000

- Write the keystore password into ``Android\my.keypass`` without a terminating newline.

- Build the APK by running ``Android\buildapk.bat``.  Note that after building, this batch script pushes the generated APK to an active instance of the Android emulator.

Additional links:

- [Play store link](https://play.google.com/store/apps/details?id=com.spaceflint.unjum&hl=en&gl=US)

- [Bluebonnet GitHub repository](https://github.com/spaceflint7/bluebonnet)

- [Bluebonnet home page](https://www.spaceflint.com/bluebonnet)

- [BNA GitHub repository](https://github.com/spaceflint7/bna)

- [Home page for Unjum](https://www.spaceflint.com/?p=207)
