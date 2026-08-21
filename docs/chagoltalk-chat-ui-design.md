# ChagolTalk Premium Anonymous Chat UI Design

## Goal
Replace the prototype conversation screen with an immersive, premium anonymous-chat experience while preserving the existing real-time SignalR architecture.

## Visual direction
- Dark, immersive full-viewport interface.
- Minimal chrome and no visible Conversation ID.
- Compact ChagolTalk header with anonymous stranger identity/status.
- Message bubbles: stranger left, current user right.
- Large bottom composer with send action.
- End Conversation is a secondary action with confirmation.
- Responsive desktop/mobile layout.
- No unnecessary cards, tables, timestamps, or dashboard-style UI.

## Functional boundaries
The UI will continue using the existing Conversation and SignalR flow.
Messages remain real-time only and are not persisted in a Message table.
The existing User1Id/User2Id naming remains unchanged.

## Implementation sequence
1. Replace Room.cshtml visual structure and styling.
2. Preserve SignalR connection and conversation joining.
3. Connect the composer to SendMessage and ReceiveMessage.
4. Add end-conversation confirmation and lifecycle handling.
5. Verify two-browser messaging and responsive behavior.
6. Only after UI/functionality is stable, implement proper database conversation state transitions.

## Success criteria
- Room looks like a polished anonymous-chat product rather than a prototype.
- Both matched users can enter the same room.
- Messages appear in real time on both sides.
- End Conversation is clear but not visually dominant.
- No Conversation ID or debug information is shown to users.
- Existing authentication, matching, SignalR, and User1Id/User2Id conventions remain intact.
