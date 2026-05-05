#include <windows.h>

int WINAPI WinMain(HINSTANCE, HINSTANCE, LPSTR, int)
{
	SHELLEXECUTEINFOA sei = { sizeof(sei) };
	sei.lpVerb = "open";
	sei.lpFile = "runtime\\MessageBroadcast.Sender.exe";
	sei.lpParameters = "";
	sei.nShow = SW_SHOWNORMAL;

	ShellExecuteExA(&sei);
}