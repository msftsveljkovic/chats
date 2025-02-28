// Dear ImGui Chat client

#include "httplib.h"

#include "imgui.h"
#include "imgui_impl_win32.h"
#include "imgui_impl_dx10.h"
#include <d3d10_1.h>
#include <d3d10.h>
#include <tchar.h>

#include "json.hpp"

#include <future>
#include <chrono>
#include <string>
#include <iostream>

// Data
static ID3D10Device*            g_pd3dDevice = nullptr;
static IDXGISwapChain*          g_pSwapChain = nullptr;
static bool                     g_SwapChainOccluded = false;
static UINT                     g_ResizeWidth = 0, g_ResizeHeight = 0;
static ID3D10RenderTargetView*  g_mainRenderTargetView = nullptr;

// Forward declarations of helper functions
bool CreateDeviceD3D(HWND hWnd);
void CleanupDeviceD3D();
void CreateRenderTarget();
void CleanupRenderTarget();
LRESULT WINAPI WndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam);


struct Message {
	std::string user;
	std::string content;
};
struct MessageList {
	uint64_t tstamp;
	std::vector<Message> msgs;
};

// Main code
int main(int, char**)
{
    // Create application window
    //ImGui_ImplWin32_EnableDpiAwareness();
    WNDCLASSEXW wc = { sizeof(wc), CS_CLASSDC, WndProc, 0L, 0L, GetModuleHandle(nullptr), nullptr, nullptr, nullptr, nullptr, L"ImGui Example", nullptr };
    ::RegisterClassExW(&wc);
    HWND hwnd = ::CreateWindowW(wc.lpszClassName, L"Dear ImGui DirectX10 Example", WS_OVERLAPPEDWINDOW, 100, 100, 680, 800, nullptr, nullptr, wc.hInstance, nullptr);

    // Initialize Direct3D
    if (!CreateDeviceD3D(hwnd))
    {
        CleanupDeviceD3D();
        ::UnregisterClassW(wc.lpszClassName, wc.hInstance);
        return 1;
    }

    // Show the window
    ::ShowWindow(hwnd, SW_SHOWDEFAULT);
    ::UpdateWindow(hwnd);

    // Setup Dear ImGui context
    IMGUI_CHECKVERSION();
    ImGui::CreateContext();
    ImGuiIO& io = ImGui::GetIO(); // (void)io;
    io.ConfigFlags |= ImGuiConfigFlags_NavEnableKeyboard;     // Enable Keyboard Controls
    io.ConfigFlags |= ImGuiConfigFlags_NavEnableGamepad;      // Enable Gamepad Controls

    // Setup Dear ImGui style
    ImGui::StyleColorsDark();
    //ImGui::StyleColorsLight();

    // Setup Platform/Renderer backends
    ImGui_ImplWin32_Init(hwnd);
    ImGui_ImplDX10_Init(g_pd3dDevice);

    // Load Fonts
    ImFont* font = io.Fonts->AddFontFromFileTTF("../../misc/fonts/Roboto-Medium.ttf", 16.0f, nullptr, io.Fonts->GetGlyphRangesCyrillic());
    //IM_ASSERT(font != nullptr);

    // Our state
    ImVec4 clear_color = ImVec4(0.45f, 0.55f, 0.60f, 1.00f);
	char baseurl[50] = "http://localhost:8472";
	char key[20] = {"33"};
	char name[20] = {"rile"};
	Message recent[40];
	size_t head = std::size(recent) - 1;

    auto incoming = [&](Message const& m) {
		auto i = (head+1)%std::size(recent);
		recent[i] = m;
		head = i;
	};
	char message[80] = {'\0'};
	bool show_settings = false;
    auto pub = [&](std::string s) {
		httplib::Client cli(baseurl);
		std::string path("/v1/pub?apiKey=");
		path += key;
		std::string body("{\"user\":\"");
		body += name;
		body += "\",\"content\":\"";
		body += s;
		body += "\"}";
		
		auto res = cli.Post(path, body, "application/json");
		if (!res) {
			std::cout << "pub no response\n";
			return false;
		}
		if (res->status != 200) {
			std::cout << "pub HTTP status: " << res->status << ", body: " << res->body << "\n";
			return false;
		}
		else {
			auto js = nlohmann::json::parse(res->body);
			if (!js.is_boolean()) {
				std::cout << "Should have been boolean, but it's: " << js << ", body: " << res->body << "\n";
				return false;
			}
			return js.get<bool>();
		}
		return true;
	};
	std::future<bool> pubfut;
	std::string status;
	uint64_t ts = 0;
	auto get = [&](uint64_t ts) {
		MessageList rslt = { 0 };
		httplib::Client cli(baseurl);
		std::string path("/v1/get?apiKey=");
		path += key;
		path += "&user=";
		path += name;
		path += "&fromTS=";
		path += std::to_string(ts);
		auto res = cli.Get(path);
		if (!res) {
			std::cout << "get no reponse\n";
			return rslt;
		}
		if (res->status != 200) {
			std::cout << "get HTTP status: " << res->status << ", body: " << res->body << "\n";
			return rslt;
		}
		else {
			auto js = nlohmann::json::parse(res->body);
			rslt.tstamp = js.value("tstamp", uint64_t(0));
			if (js.contains("msgs")) {
				auto msgs = js["msgs"];
				for (auto& m : msgs) {
					Message rcv;
					rcv.user = m.value<std::string>("user", "unown");
					rcv.content = m.value<std::string>("content", "<empty>");
					rslt.msgs.push_back(rcv);
				}
				return rslt;
			}
			else {
				std::cout << "get body does not have 'msgs', body: " << res->body << "\n";
				return rslt;
			}
		}
	};
	std::future<MessageList> getfut;
	getfut = std::async(std::launch::async, get, ts);

	auto mine = [&](Message const&m) {
		std::string text;
		text += m.user;
		text += ": ";
		text += m.content;
		auto ww = ImGui::GetWindowWidth();
		auto tw = ImGui::CalcTextSize(text.c_str()).x;
		auto sw = ImGui::GetStyle().ScrollbarSize;
		ImGui::SetCursorPosX(ww - tw - sw - ImGui::GetStyle().ItemSpacing.x * 0.5f);
		ImGui::PushStyleColor(ImGuiCol_Text, IM_COL32(0,255,255,255));
		ImGui::Text("%s", text.c_str());
		ImGui::PopStyleColor();
	};

	auto who = [&]() {
		std::vector<std::string> rslt;
		httplib::Client cli(baseurl);
		std::string path("/v1/who?apiKey=");
		path += key;
		auto res = cli.Get(path);
		if (!res) {
			std::cout << "get no reponse\n";
			return rslt;
		}
		if (res->status != 200) {
			std::cout << "get HTTP status: " << res->status << ", body: " << res->body << "\n";
			return rslt;
		}
		else {
			auto js = nlohmann::json::parse(res->body);
			for (auto& usr : js) {
				rslt.push_back(usr);
			}
			return rslt;
		}
	};
	std::future<std::vector<std::string>> whofut;
	std::vector<std::string> attendence;
	bool show_attendence = false;
	
    // Main loop
    bool done = false;
    while (!done)
    {
        // Poll and handle messages (inputs, window resize, etc.)
        // See the WndProc() function below for our to dispatch events to the Win32 backend.
        MSG msg;
        while (::PeekMessage(&msg, nullptr, 0U, 0U, PM_REMOVE))
        {
            ::TranslateMessage(&msg);
            ::DispatchMessage(&msg);
            if (msg.message == WM_QUIT)
                done = true;
        }
        if (done)
            break;

        // Handle window being minimized or screen locked
        if (g_SwapChainOccluded && g_pSwapChain->Present(0, DXGI_PRESENT_TEST) == DXGI_STATUS_OCCLUDED)
        {
            ::Sleep(10);
            continue;
        }
        g_SwapChainOccluded = false;

        // Handle window resize (we don't resize directly in the WM_SIZE handler)
        if (g_ResizeWidth != 0 && g_ResizeHeight != 0)
        {
            CleanupRenderTarget();
            g_pSwapChain->ResizeBuffers(0, g_ResizeWidth, g_ResizeHeight, DXGI_FORMAT_UNKNOWN, 0);
            g_ResizeWidth = g_ResizeHeight = 0;
            CreateRenderTarget();
        }

        // Start the Dear ImGui frame
        ImGui_ImplDX10_NewFrame();
        ImGui_ImplWin32_NewFrame();
        ImGui::NewFrame();

        // Show a simple window that we create ourselves. We use a Begin/End pair to create a named window.
        {
            static int counter = 100;

            ImGui::Begin("Hello, Chat!");

			if (getfut.valid()) {
				using namespace std::chrono_literals;
				if (getfut.wait_for(1us) == std::future_status::ready) {
					auto ml = getfut.get();
					ts = ml.tstamp + 1;
					for (auto &m : ml.msgs) {
						incoming(m);
					}
					getfut = std::async(std::launch::async, get, ts);
				}
			}
            ImGui::BeginChild("chat", ImVec2(0, 500.0f));
			for (size_t i = (head+1) % std::size(recent); i != head; i = (i+1)%std::size(recent)) {
				if (recent[i].user == name) {
					mine(recent[i]);
				}
				else {
					ImGui::Text("%s: %s", recent[i].user.c_str(), recent[i].content.c_str());
				}
			}
			if (recent[head].user == name) {
				mine(recent[head]);
			}
			else {
				ImGui::Text("%s: %s", recent[head].user.c_str(), recent[head].content.c_str());
			}
			ImGui::EndChild();
			
			ImGui::InputText("##", message, IM_ARRAYSIZE(message));
			ImGui::SameLine();
			if (ImGui::Button("Send")) {
				if (message[0] != '\0') { 
					pubfut = std::async(std::launch::async, pub, std::string(message));
					message[0] = '\0';
				}
			}
			if (pubfut.valid()) {
				using namespace std::chrono_literals;
				if (pubfut.wait_for(1us) == std::future_status::ready) {
					if (!pubfut.get()) {
						status = "Failed to publish!";
					}
					else {
						status.clear();
					}
				}
			}
            ImGui::Text(status.c_str());
            if (ImGui::Button("Settings"))
                show_settings = !show_settings;
			ImGui::SameLine();
            if (ImGui::Button("Attendence")) {
                show_attendence = !show_attendence;
				if (show_attendence && !whofut.valid()) {
					whofut = std::async(std::launch::async, who);
				}
			}
			ImGui::SameLine();
            if (ImGui::Button("Count")) {
				Message m;
				m.user = name;
				m.content = std::to_string(counter++);
				incoming(m);
			}
			ImGui::SameLine();
            ImGui::Text("counter = %d", counter);

			if (whofut.valid()) {
				using namespace std::chrono_literals;
				if (whofut.wait_for(1us) == std::future_status::ready) {
					attendence = whofut.get();
					if (attendence.empty()) {
						status = "Where did everybody go!?";
					}
				}
			}
			if (show_attendence) {
				for (auto &usr : attendence) {
					ImGui::Text(usr.c_str());
				}
			}
			if (show_settings) {
				ImGui::InputText("key", key, IM_ARRAYSIZE(key));
				ImGui::InputText("name", name, IM_ARRAYSIZE(key));
			}

            ImGui::Text("Application average %.3f ms/frame (%.1f FPS)", 1000.0f / io.Framerate, io.Framerate);
            ImGui::End();
        }

        // Rendering
        ImGui::Render();
        const float clear_color_with_alpha[4] = { clear_color.x * clear_color.w, clear_color.y * clear_color.w, clear_color.z * clear_color.w, clear_color.w };
        g_pd3dDevice->OMSetRenderTargets(1, &g_mainRenderTargetView, nullptr);
        g_pd3dDevice->ClearRenderTargetView(g_mainRenderTargetView, clear_color_with_alpha);
        ImGui_ImplDX10_RenderDrawData(ImGui::GetDrawData());

        // Present
        HRESULT hr = g_pSwapChain->Present(1, 0);   // Present with vsync
        //HRESULT hr = g_pSwapChain->Present(0, 0); // Present without vsync
        g_SwapChainOccluded = (hr == DXGI_STATUS_OCCLUDED);
    }

    // Cleanup
    ImGui_ImplDX10_Shutdown();
    ImGui_ImplWin32_Shutdown();
    ImGui::DestroyContext();

    CleanupDeviceD3D();
    ::DestroyWindow(hwnd);
    ::UnregisterClassW(wc.lpszClassName, wc.hInstance);

    return 0;
}

// Helper functions

bool CreateDeviceD3D(HWND hWnd)
{
    // Setup swap chain
    DXGI_SWAP_CHAIN_DESC sd;
    ZeroMemory(&sd, sizeof(sd));
    sd.BufferCount = 2;
    sd.BufferDesc.Width = 0;
    sd.BufferDesc.Height = 0;
    sd.BufferDesc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
    sd.BufferDesc.RefreshRate.Numerator = 60;
    sd.BufferDesc.RefreshRate.Denominator = 1;
    sd.Flags = DXGI_SWAP_CHAIN_FLAG_ALLOW_MODE_SWITCH;
    sd.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
    sd.OutputWindow = hWnd;
    sd.SampleDesc.Count = 1;
    sd.SampleDesc.Quality = 0;
    sd.Windowed = TRUE;
    sd.SwapEffect = DXGI_SWAP_EFFECT_DISCARD;

    UINT createDeviceFlags = 0;
    //createDeviceFlags |= D3D10_CREATE_DEVICE_DEBUG;
    HRESULT res = D3D10CreateDeviceAndSwapChain(nullptr, D3D10_DRIVER_TYPE_HARDWARE, nullptr, createDeviceFlags, D3D10_SDK_VERSION, &sd, &g_pSwapChain, &g_pd3dDevice);
    if (res == DXGI_ERROR_UNSUPPORTED) // Try high-performance WARP software driver if hardware is not available.
        res = D3D10CreateDeviceAndSwapChain(nullptr, D3D10_DRIVER_TYPE_WARP, nullptr, createDeviceFlags, D3D10_SDK_VERSION, &sd, &g_pSwapChain, &g_pd3dDevice);
    if (res != S_OK)
        return false;

    CreateRenderTarget();
    return true;
}

void CleanupDeviceD3D()
{
    CleanupRenderTarget();
    if (g_pSwapChain) { g_pSwapChain->Release(); g_pSwapChain = nullptr; }
    if (g_pd3dDevice) { g_pd3dDevice->Release(); g_pd3dDevice = nullptr; }
}

void CreateRenderTarget()
{
    ID3D10Texture2D* pBackBuffer;
    g_pSwapChain->GetBuffer(0, IID_PPV_ARGS(&pBackBuffer));
    g_pd3dDevice->CreateRenderTargetView(pBackBuffer, nullptr, &g_mainRenderTargetView);
    pBackBuffer->Release();
}

void CleanupRenderTarget()
{
    if (g_mainRenderTargetView) { g_mainRenderTargetView->Release(); g_mainRenderTargetView = nullptr; }
}

// Forward declare message handler from imgui_impl_win32.cpp
extern IMGUI_IMPL_API LRESULT ImGui_ImplWin32_WndProcHandler(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam);

// Win32 message handler
// You can read the io.WantCaptureMouse, io.WantCaptureKeyboard flags to tell if dear imgui wants to use your inputs.
// - When io.WantCaptureMouse is true, do not dispatch mouse input data to your main application, or clear/overwrite your copy of the mouse data.
// - When io.WantCaptureKeyboard is true, do not dispatch keyboard input data to your main application, or clear/overwrite your copy of the keyboard data.
// Generally you may always pass all inputs to dear imgui, and hide them from your application based on those two flags.
LRESULT WINAPI WndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    if (ImGui_ImplWin32_WndProcHandler(hWnd, msg, wParam, lParam))
        return true;

    switch (msg)
    {
    case WM_SIZE:
        if (wParam == SIZE_MINIMIZED)
            return 0;
        g_ResizeWidth = (UINT)LOWORD(lParam); // Queue resize
        g_ResizeHeight = (UINT)HIWORD(lParam);
        return 0;
    case WM_SYSCOMMAND:
        if ((wParam & 0xfff0) == SC_KEYMENU) // Disable ALT application menu
            return 0;
        break;
    case WM_DESTROY:
        ::PostQuitMessage(0);
        return 0;
    }
    return ::DefWindowProcW(hWnd, msg, wParam, lParam);
}
