// Inno runs this Windows helper under Wine. Signing stays in the Linux parent
// process; credentials are never passed on the compiler command line.
#define WIN32_LEAN_AND_MEAN
#include <winsock2.h>
#include <windows.h>
#include <cstdlib>
#include <string>

int main(int argc, char** argv) {
  const char* port = std::getenv("SIGN_PORT");
  const char* token = std::getenv("SIGN_TOKEN");
  if (argc != 2 || !port || !token) return 1;
  WSADATA data;
  if (WSAStartup(MAKEWORD(2, 2), &data)) return 1;
  SOCKET connection = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
  if (connection == INVALID_SOCKET) return 1;
  DWORD timeout = 180000;
  setsockopt(connection, SOL_SOCKET, SO_RCVTIMEO,
             reinterpret_cast<const char*>(&timeout), sizeof(timeout));
  sockaddr_in address{};
  address.sin_family = AF_INET;
  address.sin_port = htons(static_cast<unsigned short>(std::atoi(port)));
  address.sin_addr.s_addr = htonl(INADDR_LOOPBACK);
  if (connect(connection, reinterpret_cast<sockaddr*>(&address), sizeof(address))) return 1;
  std::string request = std::string(token) + "\n" + argv[1] + "\n";
  size_t sent = 0;
  while (sent < request.size()) {
    int count = send(connection, request.data() + sent,
                     static_cast<int>(request.size() - sent), 0);
    if (count <= 0) return 1;
    sent += count;
  }
  char result[3]{};
  int received = recv(connection, result, sizeof(result), MSG_WAITALL);
  closesocket(connection);
  WSACleanup();
  return received == 3 && std::string(result, 3) == "OK\n" ? 0 : 1;
}
