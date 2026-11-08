import unittest

import check_version as check


class VersionTests(unittest.TestCase):
    def test_reads_the_props_version(self):
        self.assertEqual(check.parse_version("<Project><PropertyGroup><Version>1.2.3</Version></PropertyGroup></Project>"), "1.2.3")

    def test_rejects_a_missing_or_malformed_version(self):
        for text in ("<Project/>", "<Version>1.2</Version>", "<Version>1.02.3</Version>", "<Version>1.2.3-beta</Version>"):
            with self.assertRaises(ValueError, msg=text):
                check.parse_version(text)

    def test_workflows_and_documentation_do_not_ship(self):
        self.assertFalse(check.requires_release(".github/workflows/release.yml"))
        self.assertFalse(check.requires_release("README.md"))
        self.assertFalse(check.requires_release("release-notes/0.1.0.md"))
        self.assertTrue(check.requires_release("src/Frontends/Calculator.Avalonia/App.axaml.cs"))
        self.assertTrue(check.requires_release("licenses/sbom.spdx.json"))

    def test_version_must_beat_every_release(self):
        check.require_new_version("0.1.0", "0.1.0", [])
        check.require_new_version("0.2.0", "0.1.0", ["0.1.0"])
        with self.assertRaises(ValueError):
            check.require_new_version("0.1.0", "0.1.0", ["0.1.0"])
        with self.assertRaises(ValueError):
            check.require_new_version("0.1.9", "0.1.0", ["0.1.10"])

    def test_version_compares_numerically(self):
        check.require_new_version("0.10.0", "0.2.0", ["0.9.0"])
        with self.assertRaises(ValueError):
            check.require_new_version("0.9.0", "0.2.0", ["0.10.0"])

    def test_version_may_not_drop_below_the_base(self):
        with self.assertRaises(ValueError):
            check.require_new_version("0.3.0", "0.4.0", [])

    def test_notes_are_bullet_points(self):
        check.validate_notes("- Adds a thing.\n- Fixes another.\n", "n.md")
        check.validate_notes("- One.\n\n- Two after a blank line.\n", "n.md")

    def test_notes_reject_anything_else(self):
        for content in ("", "\n\n", "Adds a thing.\n", "# Heading\n- A\n", "- A\nwrapped line\n", "-No space\n", "* Star bullet\n", "  - indented\n"):
            with self.assertRaises(ValueError, msg=repr(content)):
                check.validate_notes(content, "n.md")


if __name__ == "__main__":
    unittest.main()
