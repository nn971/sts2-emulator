#!/usr/bin/env python3
"""Generate a standalone offline native trace inspector with optional candidate comparison.

This browser timeline is a viewer of recorded semantic decisions. Live native
stepping requires the separately audited mod dispatcher.
"""
from __future__ import annotations

import argparse
import json
from html import escape
from pathlib import Path

from native_parity import compare, load_trace


def render(native: list[dict], emulator: list[dict] | None = None) -> str:
    records = [r for r in native if r.get("type") == "decision"]
    candidate = [r for r in (emulator or []) if r.get("type") == "decision"]
    reports = []
    if emulator is not None:
        for record in records:
            reports.append(compare(native, emulator, decision_limit=record["decision_id"]))
    data = {"header": native[0] if native else {}, "native": native,
            "candidate": emulator or [], "decisions": records,
            "candidate_decisions": candidate, "reports": reports}
    # Keep arbitrary trace strings from terminating the inline script.
    payload = json.dumps(data, ensure_ascii=False, separators=(",", ":")).replace("<", "\\u003c")
    return r"""<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>STS2 Native Fidelity Inspector</title>
<style>
:root{color-scheme:dark;background:#10151e;color:#e4eaf3;font-family:system-ui,Segoe UI,sans-serif}
body{margin:0;padding:22px;max-width:1400px;margin-inline:auto}
header{display:flex;align-items:center;justify-content:space-between;gap:18px;flex-wrap:wrap}
h1{font-size:1.45rem;margin:0}h2{font-size:1.1rem}
small,.muted{color:#adb8cb}
button,input{background:#202d40;color:#f4f6fa;border:1px solid #52617a;border-radius:7px;padding:8px 11px}
button{cursor:pointer}button:disabled{opacity:.45;cursor:default}
.controls{display:flex;align-items:center;gap:10px;flex-wrap:wrap;margin-block:18px}
.panel{background:#192230;border:1px solid #344257;border-radius:10px;padding:14px;margin-block:12px;min-width:0}
.pair{display:grid;grid-template-columns:repeat(auto-fit,minmax(310px,1fr));gap:12px}
pre{white-space:pre-wrap;word-break:break-word;margin:0;font-size:.82rem;max-height:450px;overflow:auto}
.tag{display:inline-block;padding:4px 9px;border-radius:6px;background:#334257}
.bad{background:#62283c}.good{background:#25543a}.warn{background:#725423}
.timeline{display:flex;flex-direction:column;gap:7px}
.event{border-left:3px solid #52617a;padding:7px 10px;background:#232e40;border-radius:5px}
.event.different{border-color:#ff9b5e;background:#463225}
.event>summary{cursor:pointer;font-size:.9rem}
.columns{display:grid;grid-template-columns:repeat(auto-fit,minmax(325px,1fr));gap:12px}
</style></head>
<body>
<header><div><h1>STS2 Native Fidelity Inspector</h1><small id="build"></small></div>
<span class="tag" id="session">Offline trace review</span></header>
<div class="controls">
<button id="previous">◀ Previous decision</button>
<label>Decision <input type="number" id="index" min="1" style="width:85px"></label>
<button id="next">Next decision ▶</button>
<span class="tag" id="phase"></span>
<button id="download">Export current report</button>
</div>
<div class="panel"><div id="status"></div><p class="muted" id="coverage"></p>
<div id="difference"></div></div>
<div class="pair"><section class="panel"><h2>Native state</h2><pre id="native-state"></pre></section>
<section class="panel"><h2>Emulator state</h2><pre id="emulator-state"></pre></section></div>
<div class="columns"><section class="panel"><h2>Selected action / legal choices</h2>
<pre id="action"></pre></section><section class="panel"><h2>Transition / RNG status</h2>
<pre id="transition"></pre></section></div>
<div class="columns"><section class="panel"><h2>Native semantic events</h2><div id="native-events" class="timeline"></div></section>
<section class="panel"><h2>Emulator semantic events</h2><div id="emulator-events" class="timeline"></div></section></div>
<script id="trace-data" type="application/json">__TRACE_JSON__</script>
<script>
"use strict";
const D=JSON.parse(document.getElementById("trace-data").textContent);
const find=(list,type,id)=>list.find(x=>x.type===type && x.decision_id===id);
const events=(list,id)=>list.filter(x=>x.type==="event" && x.decision_id===id);
const pretty=x=>x==null?"Unavailable":JSON.stringify(x,null,2);
const put=(id,value)=>{document.getElementById(id).textContent=value};
let cursor=0;
function drawEventList(id,items,differentAt) {
 const parent=document.getElementById(id);parent.replaceChildren();
 if(!items.length){const text=document.createElement("p");text.textContent="No audited events recorded";text.className="muted";parent.appendChild(text)}
 items.forEach((ev,i)=>{
   const item=document.createElement("details");item.className="event"+(i===differentAt?" different":"");
   const summary=document.createElement("summary");
   summary.textContent="#"+ev.event_seq+" "+ev.kind+"  "+(ev.actor_id||"")+" → "+(ev.target_id||"");
   const pre=document.createElement("pre");pre.textContent=pretty(ev);
   item.append(summary,pre);parent.appendChild(item);
 });
}
function draw() {
 const d=D.decisions[cursor];if(!d)return;
 const id=d.decision_id,e=D.candidate_decisions.find(x=>x.decision_id===id);
 const r=D.reports[cursor];
 document.getElementById("index").value=id;
 document.getElementById("previous").disabled=cursor===0;
 document.getElementById("next").disabled=cursor===D.decisions.length-1;
 put("phase",d.phase+" · decision "+id+"/"+D.decisions.length);
 put("native-state",pretty(d.state));put("emulator-state",pretty(e&&e.state));
 put("action",pretty({selected:find(D.native,"action",id),legal_actions:d.legal_actions}));
 put("transition",pretty({native:find(D.native,"transition",id),
    emulator:find(D.candidate,"transition",id),rng_status:d.coverage?.rng_status}));
 const status=document.getElementById("status");
 status.textContent=r ? r.status+": "+r.reason : "NATIVE CAPTURE ONLY · parity unchecked";
 status.className="tag "+(r?.status==="MATCH"?"good":r?.status==="MISMATCH"?"bad":"warn");
 put("coverage",r?"Verified fields: "+(r.coverage?.verified_fields||[]).join(", ")+
  " · missing: "+(r.coverage?.missing_fields||[]).join(", "):
  "Verified fields: "+(d.coverage?.verified_fields||[]).join(", ")+" · native oracle");
 put("difference",r?.first_difference?pretty(r.first_difference):"");
 let diffAt=-1;
 if(r?.first_difference?.path?.startsWith("$.events")) {
   const m=r.first_difference.path.match(/\[(\d+)\]/);diffAt=m?Number(m[1]):-1;
 }
 drawEventList("native-events",events(D.native,id),diffAt);
 drawEventList("emulator-events",events(D.candidate,id),diffAt);
}
document.getElementById("previous").onclick=()=>{cursor=Math.max(0,cursor-1);draw()};
document.getElementById("next").onclick=()=>{cursor=Math.min(D.decisions.length-1,cursor+1);draw()};
document.getElementById("index").onchange=e=>{
 let found=D.decisions.findIndex(d=>d.decision_id===Number(e.target.value));
 if(found>=0)cursor=found;draw();
};
document.addEventListener("keydown",e=>{
 if(e.key==="ArrowLeft")document.getElementById("previous").click();
 if(e.key==="ArrowRight")document.getElementById("next").click();
});
document.getElementById("download").onclick=()=>{
 const current=D.decisions[cursor];
 const report=D.reports[cursor]||{status:"NATIVE_ONLY",decision_id:current.decision_id,
   native_prefix:D.native.filter(x=>x.decision_id<=current.decision_id||x.type==="header")};
 const blob=new Blob([JSON.stringify(report,null,2)],{type:"application/json"});
 const url=URL.createObjectURL(blob),a=document.createElement("a");a.href=url;
 a.download="sts2-inspector-decision-"+current.decision_id+".json";a.click();
 setTimeout(()=>URL.revokeObjectURL(url),1000);
};
put("build",D.header.game_build+" · bridge "+D.header.bridge_version+
  " · trace schema "+D.header.trace_schema+" · "+D.header.run_id);
put("session",D.header.capture_mode||"Recorded trace");
draw();
</script></body></html>
""".replace("__TRACE_JSON__", payload)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("native", type=Path)
    parser.add_argument("--emulator", type=Path)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    native, error = load_trace(args.native)
    if error:
        parser.error("Native trace is incomplete: " + error)
    emulator = None
    if args.emulator:
        emulator, error = load_trace(args.emulator)
        if error:
            parser.error("Emulator trace is incomplete: " + error)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(render(native, emulator), encoding="utf-8")
    print(args.out)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
