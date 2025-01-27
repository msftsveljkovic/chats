= Chat API SF App demo

Chat app API demo consists of several services:

- Chat REST API, which receives the REST requests and forwards them to
  services that a particular call concerns. It also gives weights to
  each of them, keeps this in a running some as a metric which is reported
  and configured to do auto-scaling.
- History service which keeps a list of recent messages for each API key.
  There's an "amount" limit to how much it keeps but also a term limit how
  long messages are kept.
- User service keeps a list of active users. One need not publish, but just
  reading is considered an activity. After a period of inactivity, inactive
  users are removed from the list.
- Ide is an Actor service, which implements a "bot" which, when mentioned in
  the chat, starts singing a well known Serbian popular folk/country song
  "Ide Mile Lajkovackom Prugom" (Here Goes Mile on the Lajkovac Railway).

There's also a helper `Comm` library which holds the interfaces used for 
communication between services.