# Lake Country Spanish - Feature Implementation Spec

## Implementation Status

| Phase | Feature | Status |
|-------|---------|--------|
| 1 | Subscription Foundation | **COMPLETE** |
| 2 | Token System | Planned |
| 3 | Points & Gamification | Planned |
| 4 | AI Assignments | Planned |
| 5 | Analytics & Polish | Planned |

### Phase 1 Completed Features
- SubscriptionTier, Subscription, RecurringSchedule, SubscriptionHistory entities
- Stripe Checkout integration for subscription creation
- Webhook handling for subscription lifecycle events
- Customer Portal integration for billing management
- Recurring schedule management with auto-class generation
- Subscription management UI (Plans, Manage, Success views)
- Cancellation policy enforcement (3rd week deadline)
- Pause/Resume subscription functionality

---

## Core Business Model

### Subscription System (Primary Revenue)
- **Multiple subscription tiers**
  - 2 classes/month
  - 4 classes/month
  - 8 classes/month
  - Custom tiers (admin configurable)
  
- **Subscription features**
  - Recurring monthly billing via Stripe
  - Auto-schedules claimed timeslots
  - Students select recurring schedule (e.g., "Every Tuesday 6pm, Thursday 7pm")
  - Auto-renewal on 1st of month
  - Cancellation requires notice by 3rd week of month or committed to next month
  - Pause/resume functionality
  - Per-class cost lower than token purchase (incentivize subscriptions)

- **Pricing structure**
  - Single token: $30/class
  - 4 classes/month subscription: $80 ($20/class - 33% discount)
  - 8 classes/month subscription: $140 ($17.50/class - 42% discount)
  - Display savings prominently: "Save 33% with subscription!"

### Token System (Supplementary)

**Earned Tokens (unlimited use, free)**
- Earned through completing exercises and earning badges
- 100 points = 1 earned token
- Can be used for:
  - Bonus classes beyond subscription
  - Makeup classes for missed subscription sessions
  - Special workshops or group classes
  - Guest passes (bring a friend)
- Never expire
- Subscribers earn points at 1.5x rate (incentive to subscribe)

**Purchased Tokens (Karen-controlled, limited use)**
- Karen must enable purchase permission for each student
- Permission includes:
  - Maximum token limit
  - Expiration date
  - Tracking of purchased count
  - Granted reason: "trial" | "additional" | "admin"
- More expensive than subscription per-class rate ($30/token)
- Primary uses:
  - Trial classes for new students (2-3 tokens, 30-day expiration)
  - Additional classes for subscribers beyond their plan (Karen approves case-by-case, 14-day expiration)
- Permission auto-expires when limit reached or expiration date passes
- When permission not active, show: "To purchase additional classes, please contact Karen"

## Points & Gamification System

### Points Economy
- 1 point per correct answer (standard)
- 2 points for challenging questions (flagged by Karen or system)
- Bonus points for first-attempt correct answers
- Small completion bonus even if not perfect (encourages completion)
- 100 points = 1 earned token
- Points contribute to both token earning AND badge progress (dual-track)

### Badge System

**Milestone Badges**
- "First 100 Points"
- "500 Point Milestone"
- "1,000 Point Achievement"
- Gives students rewards before first earned token

**Topic Mastery Badges**
- "Preterite Master"
- "Subjunctive Survivor"
- "Vocabulary Virtuoso"
- Awarded after X correct answers in specific topic categories

**Consistency Badges**
- "7-Day Streak"
- "30-Day Streak"
- "Early Bird" (assignment completed before 9am)
- "Night Owl" (assignment completed after 9pm)

**Challenge Badges**
- "Perfect Score" (100% on assignment)
- "Speed Round" (completed under time with >90% accuracy)

**Level Progress Badges**
- "A1 Graduate"
- "B1 Warrior"
- "B2 Champion"
- Awarded when passing threshold test to advance levels

### Student Dashboard Display
- Current point total (prominent)
- Progress bar: "X/100 points until next token"
- Badges earned (displayed prominently)
- Badges close to earning: "50 more points until [badge name]"
- Current streak count
- Next subscription class scheduled

## User Management & Permissions

### Account Creation (Admin Only)
- Only Karen can create student accounts
- Karen sets initial permissions during creation:
  - Grant free trial credits (direct)
  - Enable trial token purchase (with limit and expiration)
  - Set CEFR level (A1, A2, B1, B2, C1, C2)
  - Set initial curriculum focus areas

### Student Profile Flags

```
Student {
  // Always available
  can_view_subscriptions: true (always)
  can_purchase_subscription: true (always)
  can_use_earned_tokens: true (always)
  
  // Karen-controlled
  token_purchase_permission: {
    enabled: boolean,
    limit: int,
    expires_at: datetime,
    purchased_count: int,
    granted_by: "trial" | "additional" | "admin"
  }
  
  // Status tracking
  has_active_subscription: boolean,
  subscription_tier: string,
  recurring_schedule: array,
  cefr_level: string,
  earned_tokens: int,
  total_points: int,
  current_streak: int,
  badges_earned: array
}
```

## Assignment Auto-Generation System

### Initial Setup (Karen)
- Create curriculum outline per CEFR level (A1-C2)
- Define major topic areas per level:
  - Grammar topics (e.g., "past tense", "subjunctive mood")
  - Vocabulary themes (e.g., "food", "travel", "business")
  - Skill areas (reading, writing, listening, speaking)

### Student Profiling
- Pre-test determines initial CEFR level
- Track struggle areas after each assignment
- Simple feedback after assignments: "Too easy / Just right / Too hard"
- "I need more practice with:" [verbs/vocabulary/listening/etc]

### Generation Parameters (per student)
- Current CEFR level
- Current focus area (from Karen's curriculum)
- Preferred exercise types:
  - Fill-in-the-blank
  - Translation exercises
  - Conversation prompts
  - Listening comprehension
  - Multiple choice
- Difficulty adjustment based on recent performance
- Topics they're currently working on (Karen sets)

### AI Generation Implementation
- Use Claude API for exercise generation
- Prompt template example:
  ```
  Generate 5 fill-in-the-blank exercises for [LEVEL]-level Spanish students
  focusing on [TOPIC]. Include answer key.
  Context: Student struggles with [SPECIFIC_ISSUE].
  Format as JSON for easy parsing.
  ```
- Generate assignments with:
  - Exercise questions
  - Correct answers
  - Point values per question
  - Difficulty rating
  - Topic tags

### Quality Control
- Karen reviews first batch of generated content per level/topic
- Flag certain assignments for Karen review before going live
- Karen can approve/reject/edit generated assignments
- Save approved assignments to content library

### Content Library
- Store successfully-generated and approved assignments
- Build library over time
- Mix auto-generated with Karen's signature assignments
- Tag assignments by: level, topic, difficulty, exercise type
- Reuse library content before generating new

### Assignment Display & Tracking
- Show assignment title, estimated time, point value
- Track completion status
- Track performance (% correct, time taken)
- Award points immediately upon submission
- Show feedback and correct answers after submission
- Track which topics student struggles with

## Class Scheduling System

### For Subscribers
- Recurring schedule auto-creates classes monthly
- Example: "Every Tuesday 6pm, Thursday 7pm" = 8 classes/month
- Auto-schedules on 1st of month for entire month
- Students can reschedule individual instances (within month)
- Missed class without cancellation converts to earned token (configurable)
- Clear calendar view showing all scheduled classes

### For Token Users (Trial/Additional)
- Browse Karen's available timeslots
- Select time, spend 1 token to book
- Confirmation email sent
- Cancellation policy (X hours notice to refund token)

### Karen's Schedule Management
- View all scheduled classes (color-coded: subscription vs token)
- Block out unavailable times
- Set recurring availability windows
- Approve/decline schedule change requests
- View student no-show history

## Admin (Karen) Controls

### Dashboard Overview
- Monthly recurring revenue (MRR)
- Active subscriptions count
- Trial students in pipeline
- Classes scheduled this week/month
- Student engagement metrics (points earned, assignments completed)

### Student Management
- List all students with filters (active subscribers, trial, inactive)
- View individual student:
  - Subscription status and history
  - Points, tokens, badges
  - Class attendance record
  - Assignment completion rate
  - Payment history
- Quick actions:
  - Grant free credits
  - Enable token purchase (set limit and expiration)
  - Adjust CEFR level
  - Add notes about student
  - Send message to student

### Content Management
- Review pending auto-generated assignments
- Approve/reject/edit assignments
- Create custom assignments manually
- Manage curriculum outlines per level
- View assignment performance metrics (which are too hard/easy)

### Financial Reports
- Revenue by month
- Subscription vs token revenue breakdown
- Student lifetime value
- Churn rate tracking
- Stripe payment reconciliation

## Stripe Integration

### Required Functionality
- **Subscription management**
  - Create subscription products with multiple price tiers
  - Handle subscription creation, updates, cancellations
  - Manage pause/resume
  - Handle failed payments and dunning
  - Prorated charges for mid-month upgrades

- **One-time payments**
  - Token purchases (when permission enabled)
  - Custom amounts if needed

- **Webhooks**
  - subscription.created
  - subscription.updated
  - subscription.deleted
  - invoice.payment_succeeded
  - invoice.payment_failed
  - customer.subscription.trial_will_end

- **Customer Portal**
  - Allow students to manage their payment methods
  - View billing history
  - Update subscription tier
  - Cancel subscription (with notice period enforcement)

## Database Schema Considerations

### Key Tables
- **users** (students and Karen)
- **subscriptions** (Stripe subscription data, recurring schedule)
- **token_transactions** (earned and purchased tokens, usage history)
- **points_transactions** (audit trail of all point awards)
- **badges** (badge definitions)
- **user_badges** (badges earned by students)
- **classes** (scheduled classes, attendance tracking)
- **assignments** (generated and custom assignments)
- **assignment_submissions** (student completions, scores)
- **curriculum_topics** (Karen's curriculum outline)
- **content_library** (approved assignments for reuse)

### Important Tracking
- Track point sources: `points_from_exercises`, `points_from_attendance`, `points_from_bonuses`
- Track token source: `earned` vs `purchased`
- Track subscription history for retention analysis
- Track assignment difficulty feedback for tuning generation

## Implementation Priority Phases

### Phase 1: Core Foundation
1. User authentication and account creation (Karen only)
2. Basic student profiles with permission flags
3. Stripe integration for subscriptions
4. Subscription purchase flow
5. Recurring class scheduling for subscribers

### Phase 2: Token System
1. Token purchase permission system (Karen grants)
2. Trial token purchase flow
3. Earned token system (basic point tracking)
4. Token usage for class booking
5. Karen admin controls for granting tokens/permissions

### Phase 3: Gamification
1. Points system (award on correct answers)
2. Point-to-token conversion (100:1)
3. Badge definitions and award logic
4. Student dashboard with points/badges display
5. Streak tracking

### Phase 4: Assignment Generation
1. CEFR level testing/assignment
2. Curriculum topic management (Karen)
3. Claude API integration for generation
4. Assignment display and submission
5. Auto-grading and point awards
6. Content library for reuse

### Phase 5: Polish & Analytics
1. Karen's comprehensive dashboard
2. Student engagement analytics
3. Financial reporting
4. Email notifications (class reminders, point milestones, etc.)
5. Assignment difficulty tuning based on feedback

## Notes for Implementation

- **Security**: Karen's admin functions need proper authentication/authorization
- **Mobile-friendly**: Students will likely access on phones between classes
- **Email notifications**: Class reminders, payment confirmations, milestone achievements
- **Cancellation policy**: Clear notice requirements for subscription cancellations
- **Token expiration**: Only purchased tokens expire (when permission expires), earned tokens never expire
- **Testing**: Stripe has test mode - use extensively before going live
- **GDPR/Privacy**: If Karen has international students, consider data privacy requirements
- **Rate limiting**: Claude API calls for assignment generation