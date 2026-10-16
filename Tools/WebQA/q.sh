#!/bin/bash
# driver.js에 명령 보내기: q.sh <shot|click|drag|wheel|eval|logs|reload|wait|quit> '<json>'
# usage: q.sh endpoint 'json'
curl -sS -m 300 -X POST "http://127.0.0.1:${QA_PORT:-8777}/$1" -d "${2:-{\}}"; echo
