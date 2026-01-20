#ifndef PCH_H
#define PCH_H

#include <windows.h>
#include <credentialprovider.h>
#include <shlwapi.h>
#include <winhttp.h>
#include <comdef.h>
#include <string>
#include <memory>
#include <thread>
#include <atomic>
#include <mutex>
#include <vector>
#include <stdlib.h>
#include <vector>
#include <sstream>
#include <iomanip>
#include <wincred.h>
#include <ntsecapi.h>

#pragma comment(lib, "winhttp.lib")
#pragma comment(lib, "shlwapi.lib")

// Forward declarations
extern void DllAddRef();
extern void DllRelease();

#endif //PCH_H

