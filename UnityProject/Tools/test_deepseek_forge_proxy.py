"""Offline contract tests: no DeepSeek request is sent and no API key is needed."""

import json
import unittest
from unittest.mock import patch

import deepseek_forge_proxy as forge


GAME_REQUEST = {
    "prompt": "希望土豆剑向前刺击",
    "characterId": "farmer",
    "ownedWeaponIds": ["sword"],
    "ownedModificationIds": [],
    "allowedCandidates": [{
        "id": "sword_focused_thrust",
        "name": "磨锋：直刺",
        "weaponId": "sword",
        "effect": "改为前刺",
    }],
}


class FakeResponse:
    def __init__(self, value):
        self.value = value

    def __enter__(self):
        return self

    def __exit__(self, *_):
        return False

    def read(self, _limit):
        return json.dumps(self.value).encode("utf-8")


def deepseek_envelope(candidate_ids):
    return {
        "choices": [{
            "finish_reason": "stop",
            "message": {"content": json.dumps({
                "candidateIds": candidate_ids,
                "explanation": "把宽幅攻击改成前刺。",
            })},
        }],
    }


class ForgeContractTests(unittest.TestCase):
    def test_valid_selection_uses_json_mode_and_catalog(self):
        captured = []

        def fake_urlopen(request, timeout):
            captured.append((request, timeout))
            return FakeResponse(deepseek_envelope(["sword_focused_thrust"]))

        with patch.object(forge, "urlopen", fake_urlopen):
            result = forge.ask_deepseek(GAME_REQUEST, "test-only-key", "deepseek-flash")
        self.assertEqual(result["candidateIds"], ["sword_focused_thrust"])
        body = json.loads(captured[0][0].data)
        self.assertEqual(body["response_format"], {"type": "json_object"})
        self.assertEqual(body["model"], "deepseek-flash")
        self.assertEqual(captured[0][1], 20)

    def test_invented_id_is_rejected(self):
        with patch.object(forge, "urlopen", return_value=FakeResponse(deepseek_envelope(["invented_script"]))):
            with self.assertRaisesRegex(ValueError, "目录外"):
                forge.ask_deepseek(GAME_REQUEST, "test-only-key", "deepseek-flash")

    def test_incomplete_answer_is_rejected(self):
        response = deepseek_envelope(["sword_focused_thrust"])
        response["choices"][0]["finish_reason"] = "length"
        with patch.object(forge, "urlopen", return_value=FakeResponse(response)):
            with self.assertRaisesRegex(ValueError, "不完整"):
                forge.ask_deepseek(GAME_REQUEST, "test-only-key", "deepseek-flash")

    def test_duplicate_catalog_ids_are_rejected_before_network(self):
        request = dict(GAME_REQUEST)
        request["allowedCandidates"] = GAME_REQUEST["allowedCandidates"] * 2
        with patch.object(forge, "urlopen") as network:
            with self.assertRaisesRegex(ValueError, "重复"):
                forge.ask_deepseek(request, "test-only-key", "deepseek-flash")
            network.assert_not_called()


if __name__ == "__main__":
    unittest.main()
