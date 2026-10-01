package main

import "testing"

func TestBuiltInExchangeCatalogSchema(t *testing.T) {
	cases := []struct {
		name, body string
		valid      bool
	}{
		{"zero catalogs", `{"entries":[]}`, true},
		{"native gear catalog", `{"entries":[{"kind":"item","id":"hood","template":"helmetHeavy1"}],"townExchangeCatalogs":[{"ownerClass":"blacksmith","offers":[{"item":"com.test:hood","name":"Hood","family":"Forge","slot":"Head"}]}]}`, true},
		{"multiple catalogs", `{"entries":[],"townExchangeCatalogs":[{"ownerClass":"blacksmith","offers":[{"item":"com.a:hood","name":"Hood","family":"Forge","slot":"Head"}]},{"ownerClass":"com.b:paladin","offers":[{"item":"com.b:shield","name":"Shield","family":"Oath","slot":"LeftHand"}]}]}`, true},
		{"legacy row declaration", `{"entries":[{"kind":"item","id":"equipment_token","template":"royalJewel","enemyDropRule":{"minimumDisplayedLevel":8,"ordinaryChancePercent":10,"bossChancePercent":50,"guaranteedByOpportunity":6}}]}`, true},
		{"class catalog without token override", `{"entries":[{"kind":"class","id":"paladin","template":"blacksmith","townExchange":{"offers":[{"item":"shield","name":"Shield","family":"Oath","slot":"LeftHand"}]}}]}`, true},
		{"empty catalog list", `{"entries":[],"townExchangeCatalogs":[]}`, false},
		{"missing owner", `{"entries":[],"townExchangeCatalogs":[{"offers":[{"item":"com.a:hood","name":"Hood","family":"Forge","slot":"Head"}]}]}`, false},
		{"duplicate offer", `{"entries":[],"townExchangeCatalogs":[{"ownerClass":"blacksmith","offers":[{"item":"com.a:hood","name":"Hood","family":"Forge","slot":"Head"},{"item":"com.a:hood","name":"Hood","family":"Forge","slot":"Head"}]}]}`, false},
	}
	for _, tc := range cases {
		t.Run(tc.name, func(t *testing.T) {
			if err := marketContent([]byte(tc.body)); (err == nil) != tc.valid {
				t.Fatalf("valid=%v error=%v", tc.valid, err)
			}
		})
	}
}
