"""Self-tests for the frozen-main vs feature TRX classifier."""
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
from stochastic_regression_report import category, compare, results


def trx(cases):
    """Create a minimal VSTest report; includes both case and class identities."""
    definitions, executions = [], []
    for idx, (test_class, name, outcome, message) in enumerate(cases):
        definitions.append(
            f'<UnitTest id="{idx}" name="{name}">'
            f'<TestMethod className="{test_class}" name="{name}"/>'
            '</UnitTest>')
        executions.append(
            f'<UnitTestResult testId="{idx}" testName="{name}" outcome="{outcome}">'
            '<Output><ErrorInfo><Message>' + message +
            '</Message></ErrorInfo></Output></UnitTestResult>')
    return ('<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'
            '<TestDefinitions>' + ''.join(definitions) + '</TestDefinitions>'
            '<Results>' + ''.join(executions) + '</Results></TestRun>')


class TestStochasticRegressionReport(unittest.TestCase):
    def test_baseline_failures_are_not_relabelled_as_regressions(self):
        with tempfile.TemporaryDirectory() as temp:
            p = Path(temp)
            cls = "Sts2Emulator.Core.Tests.Probe"
            base = [
                (cls, "PreviouslyFailing", "Failed", "old failure"),
                (cls, "Regressed", "Passed", ""),
                (cls, "Recovered", "Failed", "old failure"),
                (cls, "Passing", "Passed", ""),
            ]
            candidate = [
                (cls, "PreviouslyFailing", "Failed", "old failure"),
                (cls, "Regressed", "Failed",
                 "Automatic hand-draw result lost its DrawPlayerHand checkpoint"),
                (cls, "Recovered", "Passed", ""),
                (cls, "Passing", "Passed", ""),
                (cls, "NewTest", "Failed", "new test fails"),
            ]
            (p / "main.trx").write_text(trx(base), encoding="utf-8")
            (p / "feature.trx").write_text(trx(candidate), encoding="utf-8")
            baseline = results(p / "main.trx")
            feature = results(p / "feature.trx")
            report = compare(baseline, feature)
            self.assertEqual(1, report["new_regressions_count"])
            self.assertEqual(1, report["shared_existing_failures_count"])
            self.assertEqual(1, report["previously_failing_now_passing_count"])
            self.assertEqual(1, report["feature_only_failures_count"])
            self.assertEqual(4, report["compared_count"])
            self.assertEqual(
                "automatic_hand_draw_checkpoint",
                report["new_regressions"][0]["category"])
            self.assertEqual(
                "Sts2Emulator.Core.Tests.Probe.Regressed",
                report["new_regressions"][0]["test"])

    def test_parameterized_names_remain_distinct(self):
        with tempfile.TemporaryDirectory() as temp:
            p = Path(temp) / "cases.trx"
            p.write_text(trx([
                ("Sts2Emulator.Core.Tests.Probe", "Trial(x: 1)",
                 "Passed", ""),
                ("Sts2Emulator.Core.Tests.Probe", "Trial(x: 2)",
                 "Failed", "different"),
            ]), encoding="utf-8")
            self.assertEqual(2, len(results(p)))

    def test_failure_categories_do_not_claim_fidelity(self):
        self.assertEqual(
            "rng_operation_contract",
            category("Legacy site requires a bounded-integer request"))
        self.assertEqual(
            "encounter_fixture_or_state_validation",
            category("Encounter x is ineligible at act 2, floor 0."))
        self.assertEqual("other_requires_triage", category("unknown failure"))


if __name__ == "__main__":
    unittest.main()
