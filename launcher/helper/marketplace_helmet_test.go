package main

import "testing"

func TestMarketHelmetHairVisibility(t *testing.T) {
	for _, visibility := range []string{`{"top":true,"bottom":true}`, `{"top":false,"bottom":false}`, `{"top":false,"bottom":true}`} {
		if err := marketContent([]byte(`{"entries":[{"kind":"item","id":"hood","template":"helmetHeavy1","helmetHairVisibility":` + visibility + `}]}`)); err != nil {
			t.Fatalf("valid visibility %s: %v", visibility, err)
		}
	}
	for _, visibility := range []string{`{}`, `{"top":true}`, `{"bottom":false}`, `{"top":null,"bottom":true}`, `{"top":"true","bottom":true}`, `{"top":true,"bottom":true,"extra":true}`} {
		if err := marketContent([]byte(`{"entries":[{"kind":"item","id":"hood","template":"helmetHeavy1","helmetHairVisibility":` + visibility + `}]}`)); err == nil {
			t.Fatalf("accepted invalid visibility %s", visibility)
		}
	}
	if err := marketContent([]byte(`{"entries":[{"kind":"weapon","id":"hood","template":"native","helmetHairVisibility":{"top":true,"bottom":true}}]}`)); err == nil {
		t.Fatal("accepted helmet hair metadata on a weapon")
	}
}
