.PHONY: test clean check-unity

# Auto-detect Unity 6000.x — override with: make test UNITY=/path/to/Unity
UNAME := $(shell uname -s)
ifeq ($(UNAME),Darwin)
  UNITY ?= $(shell ls -d /Applications/Unity/Hub/Editor/6000.*/Unity.app/Contents/MacOS/Unity 2>/dev/null | sort -V | tail -n1)
else
  UNITY ?= $(shell ls -d $(HOME)/Unity/Hub/Editor/6000.*/Editor/Unity 2>/dev/null | sort -V | tail -n1)
endif

SDK_ROOT := $(shell pwd)
TEST_ZIP := $(SDK_ROOT)/Tests/TestProject.zip
TEST_RESULTS := testresults.xml
TEST_LOG := unity-test.log

define PARSE_RESULTS_PY
import xml.etree.ElementTree as ET, sys
r = ET.parse(sys.argv[1]).getroot()
passed = r.get('passed','0'); failed = r.get('failed','0'); skipped = r.get('skipped','0')
print(f'  Passed: {passed}  Failed: {failed}  Skipped: {skipped}')
print('')
for tc in r.iter('test-case'):
    name = tc.get('name'); result = tc.get('result')
    icon = {'Passed':'+','Failed':'!','Skipped':'-'}.get(result,'?')
    print(f'  [{icon}] {name}')
    if result == 'Failed':
        msg = tc.find('.//failure/message')
        m = msg.text.strip().split(chr(10))[0][:120] if msg is not None and msg.text else ''
        print(f'      {m}')
sys.exit(int(failed) > 0)
endef
export PARSE_RESULTS_PY

check-unity:
ifeq ($(UNITY),)
	$(error Unity 6000.x not found. Install it or set UNITY=/path/to/Unity)
endif
	@echo "Using Unity: $(UNITY)"

test: check-unity
	$(eval TMPDIR := $(shell mktemp -d))
	@echo "Extracting test project to $(TMPDIR)/TestProject ..."
	@mkdir -p $(TMPDIR)/TestProject
	@unzip -q $(TEST_ZIP) -d $(TMPDIR)/TestProject
	@echo "Patching manifest.json with SDK path: $(SDK_ROOT) ..."
	@python3 -c "\
	import json, sys; \
	p = '$(TMPDIR)/TestProject/Packages/manifest.json'; \
	m = json.load(open(p)); \
	m['dependencies']['com.noctuagames.sdk'] = 'file:$(SDK_ROOT)'; \
	json.dump(m, open(p,'w'), indent=2); \
	print('  patched OK')"
	@echo "Running tests ..."
	@cd $(TMPDIR) && "$(UNITY)" \
		-batchmode \
		-nographics \
		-projectPath TestProject \
		-runTests \
		-testPlatform PlayMode \
		-testResults $(SDK_ROOT)/$(TEST_RESULTS) \
		-logFile $(SDK_ROOT)/$(TEST_LOG) \
		; TEST_EXIT=$$?; \
	echo ""; \
	echo "=== Test Results ==="; \
	if [ -f "$(SDK_ROOT)/$(TEST_RESULTS)" ]; then \
		echo "$$PARSE_RESULTS_PY" | python3 - "$(SDK_ROOT)/$(TEST_RESULTS)"; \
		PARSE_EXIT=$$?; \
	else \
		echo "  WARNING: $(TEST_RESULTS) not found"; \
		PARSE_EXIT=1; \
	fi; \
	echo "Cleaning up $(TMPDIR) ..."; \
	rm -rf $(TMPDIR); \
	exit $$PARSE_EXIT

clean:
	@rm -f $(TEST_RESULTS) $(TEST_LOG) $(TEST_LOG).meta
	@echo "Cleaned test artifacts."
