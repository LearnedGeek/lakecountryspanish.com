-- Seeds the 36 Wisconsin DPI World Languages standards (Novice-band performance
-- indicators) from the June 2019 publication.
--
-- Source: Wisconsin Standards for World Languages (WI DPI, formally adopted
-- June 3, 2019, State Superintendent Carolyn Stanford Taylor)
-- https://dpi.wi.gov/sites/default/files/imce/standards/New%20pdfs/WorldLanguagesStandards2019.pdf
--
-- Performance indicator text is quoted verbatim from the source document.
-- Codes follow the WI scheme: WL.{Category}.{LearnerPractice}.{ProficiencyLevel}.
-- Standards 4 and 5 publish only a single Novice indicator ("n+") with no
-- sub-bands; we map that to ProficiencySubLevel = 0 (Unspecified).
--
-- Enum value mappings (kept in lockstep with WisconsinStandardCategory etc.
-- enums in Models/Entities/WisconsinStandard.cs):
--   Category:        Interpretive=1, Interpersonal=2, Presentational=3, Intercultural=4, GlobalCompetence=5
--   ProficiencyBand: Novice=1, Intermediate=2, Advanced=3
--   ProficiencySub:  Unspecified=0, Low=1, Mid=2, High=3
--
-- ApplicableToK4_2 defaults true for Standards 1-4 (operative for K4-2 tagging)
-- and false for Standard 5 (Global Competence — research-oriented, written for
-- older learners; surfaced separately in the admin UI).
--
-- Idempotent via ON CONFLICT ("Code") DO NOTHING; safe to re-run.

INSERT INTO "WisconsinStandards"
    ("Code", "Category", "StandardNumber", "LearnerPractice",
     "ProficiencyBand", "ProficiencySubLevel",
     "LearnerPracticeDescriptor", "PerformanceIndicator",
     "SourceDocument", "SourcePage",
     "ApplicableToK4_2", "EffectiveDate", "CreatedAt")
VALUES
    -- ===== Standard 1: Interpretive Communication (p. 21) =====
    -- Learner Practice 1.a: Authentic informational texts
    ('WL.IT.1.a.n1', 1, 1, '1.a', 1, 1,
     'Authentic informational texts',
     'Identify memorized or familiar words when they are supported by gestures or visuals in informational texts.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 21,
     true, DATE '2019-06-03', NOW()),

    ('WL.IT.1.a.n2', 1, 1, '1.a', 1, 2,
     'Authentic informational texts',
     'Identify some basic facts from memorized words and phrases when they are supported by gestures or visuals in informational texts.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 21,
     true, DATE '2019-06-03', NOW()),

    ('WL.IT.1.a.n3', 1, 1, '1.a', 1, 3,
     'Authentic informational texts',
     'Identify the topic and some isolated facts from simple sentences in informational texts.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 21,
     true, DATE '2019-06-03', NOW()),

    -- Learner Practice 1.b: Authentic fictional texts
    ('WL.IT.1.b.n1', 1, 1, '1.b', 1, 1,
     'Authentic fictional texts',
     'Identify memorized or familiar words when they are supported by gestures or visuals in fictional texts.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 21,
     true, DATE '2019-06-03', NOW()),

    ('WL.IT.1.b.n2', 1, 1, '1.b', 1, 2,
     'Authentic fictional texts',
     'Identify some basic facts from memorized words and phrases when they are supported by gestures or visuals in fictional texts.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 21,
     true, DATE '2019-06-03', NOW()),

    ('WL.IT.1.b.n3', 1, 1, '1.b', 1, 3,
     'Authentic fictional texts',
     'Identify the topic and some isolated elements from simple sentences in short fictional texts.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 21,
     true, DATE '2019-06-03', NOW()),

    -- Learner Practice 1.c: Authentic conversations and discussions
    ('WL.IT.1.c.n1', 1, 1, '1.c', 1, 1,
     'Authentic conversations and discussions',
     'Demonstrate understanding of memorized or familiar words when they are supported by gestures or visuals in conversations.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 21,
     true, DATE '2019-06-03', NOW()),

    ('WL.IT.1.c.n2', 1, 1, '1.c', 1, 2,
     'Authentic conversations and discussions',
     'Identify some basic facts from memorized words and phrases when they are supported by gestures or visuals in conversations.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 21,
     true, DATE '2019-06-03', NOW()),

    ('WL.IT.1.c.n3', 1, 1, '1.c', 1, 3,
     'Authentic conversations and discussions',
     'Demonstrate understanding of familiar questions and statements from simple sentences in conversations.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 21,
     true, DATE '2019-06-03', NOW()),

    -- ===== Standard 2: Interpersonal Communication (p. 25) =====
    -- Learner Practice 2.a: Exchange information and ideas
    ('WL.IP.2.a.n1', 2, 2, '2.a', 1, 1,
     'Exchange information and ideas',
     'Provide information by answering a few simple questions on very familiar topics, using practiced or memorized words and phrases, with the help of gestures or visuals.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 25,
     true, DATE '2019-06-03', NOW()),

    ('WL.IP.2.a.n2', 2, 2, '2.a', 1, 2,
     'Exchange information and ideas',
     'Request and provide information by asking and answering a few simple questions on very familiar and everyday topics, using a mixture of practiced or memorized words, phrases, and simple sentences.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 25,
     true, DATE '2019-06-03', NOW()),

    ('WL.IP.2.a.n3', 2, 2, '2.a', 1, 3,
     'Exchange information and ideas',
     'Request and provide information by asking and answering practiced and some original questions on familiar and everyday topics, using simple sentences most of the time.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 25,
     true, DATE '2019-06-03', NOW()),

    -- Learner Practice 2.b: Meet needs / address situations
    ('WL.IP.2.b.n1', 2, 2, '2.b', 1, 1,
     'Meet needs / address situations',
     'Express some basic needs, using practiced or memorized words and phrases, with the help of gestures or visuals.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 25,
     true, DATE '2019-06-03', NOW()),

    ('WL.IP.2.b.n2', 2, 2, '2.b', 1, 2,
     'Meet needs / address situations',
     'Express basic needs related to familiar and everyday activities, using a mixture of practiced or memorized words, phrases, and questions.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 25,
     true, DATE '2019-06-03', NOW()),

    ('WL.IP.2.b.n3', 2, 2, '2.b', 1, 3,
     'Meet needs / address situations',
     'Interact with others to meet basic needs related to routine everyday activities, using simple sentences and questions most of the time.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 25,
     true, DATE '2019-06-03', NOW()),

    -- Learner Practice 2.c: Express preferences, opinions, feelings
    ('WL.IP.2.c.n1', 2, 2, '2.c', 1, 1,
     'Express preferences, opinions, feelings',
     'Express basic preferences or feelings, using practiced or memorized words and phrases, with the help of gestures or visuals.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 25,
     true, DATE '2019-06-03', NOW()),

    ('WL.IP.2.c.n2', 2, 2, '2.c', 1, 2,
     'Express preferences, opinions, feelings',
     'Express one''s own preferences or feelings and react to those of others, using a mixture of practiced or memorized words, phrases, and questions.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 25,
     true, DATE '2019-06-03', NOW()),

    ('WL.IP.2.c.n3', 2, 2, '2.c', 1, 3,
     'Express preferences, opinions, feelings',
     'Express, ask about, and react to preferences, feelings, or opinions on familiar topics, using simple sentences most of the time and asking questions to keep the conversation on topic.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 25,
     true, DATE '2019-06-03', NOW()),

    -- ===== Standard 3: Presentational Communication (p. 29) =====
    -- Learner Practice 3.a: Describe lives, experiences, events
    ('WL.PS.3.a.n1', 3, 3, '3.a', 1, 1,
     'Describe lives, experiences, and events',
     'Introduce self, using practiced or memorized words and phrases, with the help of gestures or visuals.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 29,
     true, DATE '2019-06-03', NOW()),

    ('WL.PS.3.a.n2', 3, 3, '3.a', 1, 2,
     'Describe lives, experiences, and events',
     'Present information about interests and activities using a mixture of practiced or memorized words, phrases, and simple sentences.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 29,
     true, DATE '2019-06-03', NOW()),

    ('WL.PS.3.a.n3', 3, 3, '3.a', 1, 3,
     'Describe lives, experiences, and events',
     'Present personal information about life and activities, using simple sentences most of the time.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 29,
     true, DATE '2019-06-03', NOW()),

    -- Learner Practice 3.b: Convey preference, opinion, persuasive argument
    ('WL.PS.3.b.n1', 3, 3, '3.b', 1, 1,
     'Convey preference, opinion, persuasive argument',
     'Express likes and dislikes using practiced or memorized words and phrases, with the help of gestures or visuals.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 29,
     true, DATE '2019-06-03', NOW()),

    ('WL.PS.3.b.n2', 3, 3, '3.b', 1, 2,
     'Convey preference, opinion, persuasive argument',
     'Express likes and dislikes on very familiar and everyday topics of interest, using a mixture of practiced or memorized words, phrases, and simple sentences.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 29,
     true, DATE '2019-06-03', NOW()),

    ('WL.PS.3.b.n3', 3, 3, '3.b', 1, 3,
     'Convey preference, opinion, persuasive argument',
     'Express preferences on familiar and everyday topics of interest, using simple sentences most of the time.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 29,
     true, DATE '2019-06-03', NOW()),

    -- Learner Practice 3.c: Inform, describe, or explain
    ('WL.PS.3.c.n1', 3, 3, '3.c', 1, 1,
     'Inform, describe, or explain',
     'Name very familiar people, places, and objects using practiced or memorized words and phrases with the help of gestures or visuals.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 29,
     true, DATE '2019-06-03', NOW()),

    ('WL.PS.3.c.n2', 3, 3, '3.c', 1, 2,
     'Inform, describe, or explain',
     'Present on very familiar and everyday topics using a mixture of practiced or memorized words, phrases, and simple sentences.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 29,
     true, DATE '2019-06-03', NOW()),

    ('WL.PS.3.c.n3', 3, 3, '3.c', 1, 3,
     'Inform, describe, or explain',
     'Present on familiar and everyday topics using simple sentences most of the time.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 29,
     true, DATE '2019-06-03', NOW()),

    -- ===== Standard 4: Intercultural Communication (p. 32) =====
    -- Standards 4 and 5 publish only Novice indicators with no sub-bands ("n+");
    -- ProficiencySubLevel = 0 (Unspecified) for all.
    ('WL.IC.4.a.n+', 4, 4, '4.a', 1, 0,
     'Cultural products and perspectives',
     'Identify, in my own and other cultures, some typical products related to familiar everyday life.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 32,
     true, DATE '2019-06-03', NOW()),

    ('WL.IC.4.b.n+', 4, 4, '4.b', 1, 0,
     'Cultural practices and perspectives',
     'Identify some typical practices, in my own and other cultures, related to familiar everyday life.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 32,
     true, DATE '2019-06-03', NOW()),

    ('WL.IC.4.c.n+', 4, 4, '4.c', 1, 0,
     'Interact with members of local/global community',
     'Communicate with others from the target culture in familiar, everyday situations using memorized language and showing basic cultural and linguistic awareness.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 32,
     true, DATE '2019-06-03', NOW()),

    ('WL.IC.4.d.n+', 4, 4, '4.d', 1, 0,
     'Use culturally appropriate behaviors',
     'Use appropriate rehearsed behaviors and recognize some obviously inappropriate behaviors in familiar, everyday situations.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 32,
     true, DATE '2019-06-03', NOW()),

    -- ===== Standard 5: Global Competence and Community Engagement (p. 34) =====
    -- Standard 5 is written for older learners (research-oriented). Marked
    -- ApplicableToK4_2 = false so it's hidden from the K4-2 tagging UI by default.
    ('WL.GCE.5.a.n+', 5, 5, '5.a', 1, 0,
     'Examine local and global issues',
     'Use evidence from domestic and international sources to address a question with significance to their local and global community.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 34,
     false, DATE '2019-06-03', NOW()),

    ('WL.GCE.5.b.n+', 5, 5, '5.b', 1, 0,
     'Integrate diverse perspectives',
     'Identify different personal and community perspectives on an issue of local and global significance.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 34,
     false, DATE '2019-06-03', NOW()),

    ('WL.GCE.5.c.n+', 5, 5, '5.c', 1, 0,
     'Exchange ideas across boundaries',
     'Exchange information and perspectives on an issue of local and global significance in linguistically and culturally appropriate ways.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 34,
     false, DATE '2019-06-03', NOW()),

    ('WL.GCE.5.d.n+', 5, 5, '5.d', 1, 0,
     'Engage to improve conditions',
     'Identify options, plan, take steps, and reflect on actions targeting an issue of local and global significance.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 34,
     false, DATE '2019-06-03', NOW()),

    ('WL.GCE.5.e.n+', 5, 5, '5.e', 1, 0,
     'Set language-learning goals',
     'Choose goals for language learning and use for personal or community life, and then monitor and reflect on progress toward those goals.',
     'WI DPI Wisconsin Standards for World Languages, June 2019', 34,
     false, DATE '2019-06-03', NOW())

ON CONFLICT ("Code") DO NOTHING;
