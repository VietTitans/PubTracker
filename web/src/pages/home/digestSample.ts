// Static content for the hero's example digest. It mirrors what the API's digest email renders
// (api/RecordService/BusinessLogic/DigestService: DigestMessageFormatter lays out the header and topic
// sections; RecordTopicClassifier owns the topic names). The optional AI summary is deliberately left out
// so the example shows the core digest. If that layout or those topic names change, update this file to
// match. Titles are fictional and were chosen so RecordTopicClassifier would sort them into these topics.

export interface ExampleRecord {
  title: string;
  intention: string;
}

export interface ExampleTopic {
  name: string;
  records: ExampleRecord[];
}

export const exampleDigest = {
  source: "Library database",
  category: "Lumbar spine, SIJ or pelvis",
  topics: [
    {
      name: "Exercise & movement-based therapy",
      records: [
        {
          title: "Home-based walking programme versus supervised exercise for chronic low back pain: a randomised trial",
          intention: "To compare pain outcomes between a home walking programme and clinic-supervised exercise.",
        },
        {
          title: "Core stability exercise in adults with recurrent lumbar pain: a pilot study",
          intention: "To test whether core stability exercise reduces recurrence over six months.",
        },
      ],
    },
    {
      name: "Systematic reviews & meta-analyses",
      records: [
        {
          title: "Graded activity for chronic low back pain: a systematic review and meta-analysis",
          intention: "To pool trial evidence on graded activity compared with usual care.",
        },
      ],
    },
    {
      name: "Psychological & biopsychosocial",
      records: [
        {
          title: "Fear-avoidance beliefs and recovery time after an acute episode of low back pain",
          intention: "To examine whether fear-avoidance beliefs predict slower recovery.",
        },
      ],
    },
  ] satisfies ExampleTopic[],
};

export const exampleRecordCount = exampleDigest.topics.reduce((n, t) => n + t.records.length, 0);
