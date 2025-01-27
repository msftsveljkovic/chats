= Chat API Client app

This is a simple native Chat app written in DrImGui. While it is "Windows only",
it's core is easily ported to another platform, as it uses only C++
standard library, DrImGui itself (which is very portable) and two well 
known and portable C++ helper libraries (httplib and nlohmanjson).

A simple build script/batch file is provided `build_win32.bat` and you should
update `INCLUDES` and `SOURCES` so that it points to your DrImGui "installation"
(sources).

All code is in `main.cpp` and almost all is in between the `// Our state` and
`// Rendering` comments. The rest of the file is DrImGui glue code, could be
factored out, but this was a simple demo app.

The URL of the Chat SF App is fixed in the code, see `char baseurl[]`.