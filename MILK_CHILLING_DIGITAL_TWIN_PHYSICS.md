# Physics Formulation & Engineering Documentation
## Milk Chilling Can Digital Twin (MATLAB R2026a)

**Project:** Development of a Low-Cost, Lightweight Milk Chilling Can for Small-Scale Dairy Farmers  
**MATLAB Installation Path:** `C:\Users\SONA K M\Documents\MATLAB\MilkChillingDigitalTwin`

---

## 1. Project Physics Overview

The Milk Chilling Digital Twin models the transient thermal performance of a $20 \text{ L}$ insulated milk chilling can equipped with an integrated Phase Change Material (PCM) thermal storage jacket. 

The physical system consists of a 5-layer concentric thermal network:
$$\text{Ambient Environment } (T_{\text{amb}} = 35\,^\circ\text{C}) \xrightarrow{\quad U_{\text{amb}} = 1/R_{\text{ins}} \quad} \text{Outer Shell / Insulation} \xrightarrow{\quad \text{Enthalpy } H \quad} \text{PCM Jacket} \xrightarrow{\quad U_{\text{pcm}} = h_{\text{conv}} A_{\text{liner}} \quad} \text{Inner Liner} \xrightarrow{\quad C_{\text{milk}} \quad} \text{Milk Core}$$

Heat flows simultaneously into the PCM jacket from two sources:
1. **Milk Chilling Heat Extraction ($\dot{Q}_{\text{pcm}}$):** Thermal energy extracted from warm milk ($30\,^\circ\text{C} \to 5\,^\circ\text{C}$) across the stainless steel inner liner.
2. **Ambient Heat Influx ($\dot{Q}_{\text{amb}}$):** Parasitic thermal energy leaking in from the hot ambient environment ($35\,^\circ\text{C}$) through the outer polyurethane insulation.

---

## 2. Design Assumptions

All numerical values listed below are explicitly categorized as **DESIGN ASSUMPTIONS** or **REQUIRES DATASHEET VALIDATION** (since a physical prototype has not yet been fabricated):

* **Inner Liner Heat Transfer Coefficient ($h_{\text{conv}}$):** $30.0 \text{ W/(m}^2\cdot\text{K)}$ [DESIGN ASSUMPTION] (`parameters.m`, L39)
* **Inner Liner Contact Surface Area ($A_{\text{inner}}$):** $0.85 \text{ m}^2$ [DESIGN ASSUMPTION] (`parameters.m`, L38)
* **Base Can Stainless Mass ($W_{\text{base}}$):** $5.0 \text{ kg}$, Cost = $\text{₹}2,400 \text{ INR}$ ($\$30 \text{ USD}$) [DESIGN ASSUMPTION] (`optimizeDesign.m`, L28-29)
* **Polyurethane Insulation Density ($\rho_{\text{ins}}$):** $40.0 \text{ kg/m}^3$, Cost = $\text{₹}12,000/\text{m}^3$ ($\$150/\text{m}^3$) [DESIGN ASSUMPTION] (`optimizeDesign.m`, L30-31)
* **Encapsulated PCM Unit Cost:** $\text{₹}640/\text{kg}$ ($\$8/\text{kg}$) [REQUIRES DATASHEET VALIDATION] (`optimizeDesign.m`, L32)
* **PCM Specific Heat Capacities:** $c_{p, \text{solid}} = 2000 \text{ J/(kg}\cdot\text{K)}$, $c_{p, \text{liquid}} = 2200 \text{ J/(kg}\cdot\text{K)}$ [REQUIRES DATASHEET VALIDATION] (`parameters.m`, L63-64)
* **PCM Latent Heat of Fusion ($L_{\text{pcm}}$):** $250,000 \text{ J/kg}$ ($250 \text{ kJ/kg}$) [REQUIRES DATASHEET VALIDATION] (`parameters.m`, L65)

---

## 3. Material Properties

| Material / Substance | Property Name | Symbol | Value | Unit | Code Source |
| :--- | :--- | :---: | :---: | :---: | :--- |
| **Milk** | Density | $\rho_{\text{milk}}$ | $1030.0$ | $\text{kg/m}^3$ | `parameters.m`, L21 |
| **Milk** | Specific Heat Capacity | $c_{p, \text{milk}}$ | $3900.0$ | $\text{J/(kg}\cdot\text{K)}$ | `parameters.m`, L22 |
| **Polyurethane** | Thermal Conductivity | $k_{\text{ins}}$ | $0.025$ | $\text{W/(m}\cdot\text{K)}$ | `parameters.m`, L46 |
| **Stainless Steel** | Thermal Conductivity | $k_{\text{liner}}$ | $16.0$ | $\text{W/(m}\cdot\text{K)}$ | `parameters.m`, L41 |
| **PCM (Organic Wax)** | Solid Specific Heat | $c_{p, \text{solid}}$ | $2000.0$ | $\text{J/(kg}\cdot\text{K)}$ | `parameters.m`, L63 |
| **PCM (Organic Wax)** | Liquid Specific Heat | $c_{p, \text{liquid}}$ | $2200.0$ | $\text{J/(kg}\cdot\text{K)}$ | `parameters.m`, L64 |
| **PCM (Organic Wax)** | Latent Heat of Fusion | $L_{\text{pcm}}$ | $250,000.0$ | $\text{J/kg}$ | `parameters.m`, L65 |

---

## 4. Geometry Equations

```text
  r_milk  = 0.180 m  (Milk Core)
  r_liner = 0.185 m  (Inner Steel Liner)
  r_pcm   = 0.210 m  (PCM Concentric Jacket)
  r_ins   = 0.230 m  (20 mm Polyurethane Insulation)
  r_shell = 0.235 m  (Outer Stainless Steel Shell)
```

1. **Milk Mass Calculation:**
   $$m_{\text{milk}} = \rho_{\text{milk}} \cdot (V_{\text{milk, L}} \cdot 10^{-3}) = 1030.0 \cdot (20.0 \cdot 10^{-3}) = 20.60 \text{ kg}$$
   * MATLAB: `p.milk.mass = p.milk.density * p.milk.volume_m3;` (`parameters.m`, L27)

2. **Milk Total Heat Capacity ($C_{\text{milk}}$):**
   $$C_{\text{milk}} = m_{\text{milk}} \cdot c_{p, \text{milk}} = 20.60 \cdot 3900.0 = 80,340 \text{ J/K}$$
   * MATLAB: `p.milk.heat_capacity = p.milk.mass * p.milk.cp;` (`parameters.m`, L28)

3. **Insulation Polyurethane Volume (Optimization Model):**
   $$V_{\text{ins}} = A_{\text{can}} \cdot x_{\text{ins, m}} = 1.0 \cdot x_{\text{ins, m}} \quad [\text{m}^3]$$
   * MATLAB: `vol_ins = p.can.surface_area * x_m;` (`optimizeDesign.m`, L46)

---

## 5. Milk Thermal Model

1. **Milk Lumped Capacitance Transient Governing ODE:**
   $$C_{\text{milk}} \frac{dT_{\text{milk}}}{dt} = -\dot{Q}_{\text{pcm}}(t)$$
   * MATLAB: `dT_milk_dt = - Q_dot_pcm(k) / C_milk;` (`completeCanModel.m`, L100)

2. **Required Milk Chilling Load to Target (30 °C $\to$ 5 °C):**
   $$Q_{\text{cool, 5C}} = C_{\text{milk}} (T_{\text{milk, init}} - T_{\text{target}}) = 80,340 \cdot (30.0 - 5.0) = 2,008,500 \text{ J } (2,008.5 \text{ kJ})$$
   * MATLAB: `p.milk.Q_cool_req_5C = p.milk.heat_capacity * (p.milk.T_init - p.milk.T_target);` (`parameters.m`, L73)

---

## 6. Heat Transfer Model

1. **Conductive Insulation Thermal Resistance ($R_{\text{ins}}$):**
   $$R_{\text{ins}} = \frac{x_{\text{ins, m}}}{k_{\text{ins}} \cdot A_{\text{can}}} = \frac{0.020}{0.025 \cdot 1.0} = 0.80 \text{ K/W}$$
   * MATLAB: `p.insulation.thermal_resistance = p.insulation.thickness_m / (p.insulation.k * p.can.surface_area);` (`parameters.m`, L49)

2. **Ambient Thermal Conductance ($U_{\text{amb}}$):**
   $$U_{\text{amb}} = \frac{1}{R_{\text{ins}}} = 1.25 \text{ W/K}$$
   * MATLAB: `p.insulation.conductance = 1.0 / p.insulation.thermal_resistance;` (`parameters.m`, L50)

3. **Milk-to-PCM Inner Conductance ($U_{\text{pcm}}$ — Physically Derived):**
   $$U_{\text{pcm}} = h_{\text{conv, inner}} \cdot A_{\text{inner}} = 30.0 \cdot 0.85 = 25.50 \text{ W/K}$$
   * MATLAB: `p.pcm.U_conductance = p.liner.h_conv * p.liner.inner_area;` (`parameters.m`, L60)

4. **Heat Removal Rate from Milk ($\dot{Q}_{\text{pcm}}$):**
   $$\dot{Q}_{\text{pcm}}(t) = \max\left(0, U_{\text{pcm}} (T_{\text{milk}}(t) - T_{\text{pcm}}(t))\right)$$
   * MATLAB: `if T_milk(k) > T_pcm(k), Q_dot_pcm(k) = U_pcm * (T_milk(k) - T_pcm(k)); else, Q_dot_pcm(k) = 0.0; end` (`completeCanModel.m`, L92-96)

5. **Ambient Heat Influx Rate ($\dot{Q}_{\text{amb}}$):**
   $$\dot{Q}_{\text{amb}}(t) = U_{\text{amb}} (T_{\text{amb}} - T_{\text{pcm}}(t))$$
   * MATLAB: `Q_dot_amb(k) = U_amb * (p.env.T_amb - T_pcm(k));` (`completeCanModel.m`, L89)

---

## 7. Insulation Model

The 1D radial heat flow through polyurethane insulation assumes quasi-steady conduction across thickness $x_{\text{ins}}$:
$$q_{\text{ins}} = \frac{k_{\text{ins}}}{x_{\text{ins}}} A_{\text{can}} (T_{\text{amb}} - T_{\text{pcm}})$$

* **Insulation Overall Heat Coefficient ($h_{\text{ins}}$):**
  $$h_{\text{ins}} = \frac{k_{\text{ins}}}{x_{\text{ins}}} = \frac{0.025}{0.020} = 1.25 \text{ W/(m}^2\cdot\text{K)}$$
  * MATLAB: `p.insulation.h_coeff = p.insulation.k / p.insulation.thickness_m;` (`parameters.m`, L51)

---

## 8. PCM Enthalpy Model

The PCM thermal storage model uses a specific enthalpy formulation $H(t) \quad [\text{J/kg}]$:

1. **Solid Sensible Capacity ($Q_{\text{sensible}}$):**
   $$Q_{\text{sensible}} = m_{\text{pcm}} \cdot c_{p, \text{solid}} \cdot (T_{\text{melt}} - T_{\text{pcm, init}}) = 3.0 \cdot 2000 \cdot (5.0 - 2.0) = 18,000 \text{ J } (18 \text{ kJ})$$
   * MATLAB: `p.pcm.Q_sensible_solid = p.pcm.mass * p.pcm.cp_solid * (p.pcm.T_melt - p.pcm.T_init);` (`parameters.m`, L68)

2. **Latent Heat Capacity ($Q_{\text{latent}}$):**
   $$Q_{\text{latent}} = m_{\text{pcm}} \cdot L_{\text{pcm}} = 3.0 \cdot 250,000 = 750,000 \text{ J } (750 \text{ kJ})$$
   * MATLAB: `p.pcm.Q_latent = p.pcm.mass * p.pcm.latent_heat;` (`parameters.m`, L69)

3. **Total Thermal Storage Capacity ($Q_{\text{total}}$):**
   $$Q_{\text{total}} = Q_{\text{sensible}} + Q_{\text{latent}} = 18,000 + 750,000 = 768,000 \text{ J } (768 \text{ kJ})$$
   * MATLAB: `p.pcm.Q_total_capacity = p.pcm.Q_sensible_solid + p.pcm.Q_latent;` (`parameters.m`, L70)

4. **Specific Enthalpy Melting Thresholds ($H_s, H_l$):**
   $$H_s = c_{p, \text{solid}} (T_{\text{melt}} - T_{\text{pcm, init}}) = 2000 \cdot (5.0 - 2.0) = 6000 \text{ J/kg}$$
   $$H_l = H_s + L_{\text{pcm}} = 6000 + 250,000 = 256,000 \text{ J/kg}$$
   * MATLAB: `H_s = cp_s * (T_melt - T_pcm_init); H_l = H_s + L_pcm;` (`completeCanModel.m`, L74-75)

5. **PCM Specific Enthalpy Differential Integration:**
   $$dH_{\text{pcm}} = \frac{\dot{Q}_{\text{amb}}(t) + \dot{Q}_{\text{pcm}}(t)}{m_{\text{pcm}}} \, dt, \quad H_{\text{pcm}}(t+\Delta t) = H_{\text{pcm}}(t) + dH_{\text{pcm}}$$
   * MATLAB: `dH_pcm = ((Q_dot_amb(k) + Q_dot_pcm(k)) / m_pcm) * dt; H_pcm_current = H_pcm_current + dH_pcm;` (`completeCanModel.m`, L104-105)

6. **PCM Temperature & Liquid Fraction ($\beta$) Evaluation:**
   $$\begin{cases}
     T_{\text{pcm}} = T_{\text{pcm, init}} + \frac{H_{\text{pcm}}}{c_{p, \text{solid}}}, \quad \beta = 0.0 & \text{if } H_{\text{pcm}} < H_s \quad \text{(Solid Phase)} \\
     T_{\text{pcm}} = T_{\text{melt}} = 5.0\,^\circ\text{C}, \quad \beta = \frac{H_{\text{pcm}} - H_s}{L_{\text{pcm}}} & \text{if } H_s \le H_{\text{pcm}} \le H_l \quad \text{(Latent Phase)} \\
     T_{\text{pcm}} = T_{\text{melt}} + \frac{H_{\text{pcm}} - H_l}{c_{p, \text{liquid}}}, \quad \beta = 1.0 & \text{if } H_{\text{pcm}} > H_l \quad \text{(Liquid Phase)}
   \end{cases}$$
   * MATLAB: (`completeCanModel.m`, L108-117)

7. **Remaining PCM Capacity ($Q_{\text{rem, pcm}}$):**
   $$Q_{\text{rem, pcm}}(t) = \max\left(0, Q_{\text{total}} - Q_{\text{cum, milk loss}}(t)\right)$$
   * MATLAB: `Q_rem_pcm(k+1) = max(0, Q_total_cap - Q_cum_milk_loss(k+1));` (`completeCanModel.m`, L122)

---

## 9. Energy Balance & Validation

1. **Cumulative Heat Energy Extracted from Milk ($Q_{\text{cum, milk loss}}$):**
   $$Q_{\text{cum, milk loss}}(t) = \int_{0}^{t} \dot{Q}_{\text{pcm}}(\tau) \, d\tau \approx \sum_{k=1}^{N-1} \dot{Q}_{\text{pcm}}(k) \cdot \Delta t$$
   * MATLAB: `Q_cum_milk_loss(k+1) = Q_cum_milk_loss(k) + Q_dot_pcm(k) * dt;` (`completeCanModel.m`, L120)

2. **First Law Energy Conservation Error Percentage:**
   $$\Delta E_{\text{milk}} = C_{\text{milk}} (T_{\text{milk, init}} - T_{\text{milk}}(t))$$
   $$\text{Err}_{\%}(t) = \frac{|\Delta E_{\text{milk}}(t) - Q_{\text{cum, milk loss}}(t)|}{\max(1.0, Q_{\text{cum, milk loss}}(t))} \cdot 100\%$$
   * MATLAB: (`completeCanModel.m`, L125-127)  
   * **Validation Result:** Max error $= 7.99 \times 10^{-12} \ \%$ (Strict First-Law Conservation Verified).

---

## 10. Time Integration

* **Method:** Explicit First-Order Euler Method ($\Delta t = 1.0 \text{ s}$)
* **Time Vector:** $t \in [0, 28,800] \text{ s}$ ($0 \text{ to } 8.0 \text{ hours}$)
* **State Updates:**
  $$T_{\text{milk}}(k+1) = T_{\text{milk}}(k) + \left(\frac{-\dot{Q}_{\text{pcm}}(k)}{C_{\text{milk}}}\right) \cdot \Delta t$$

---

## 11. Multi-Objective Optimization Model

The optimizer evaluates candidate design pairs $(x_{\text{ins, mm}}, m_{\text{pcm, kg}})$ across a grid search sweep ($x_{\text{ins}} \in [10, 40] \text{ mm}, m_{\text{pcm}} \in [0, 10] \text{ kg}$):

1. **System Weight Model [DESIGN ASSUMPTION]:**
   $$W_{\text{total}} = W_{\text{base}} + w_{\text{ins}} + m_{\text{pcm}} = 5.0 + (40.0 \cdot 1.0 \cdot x_{\text{ins, m}}) + m_{\text{pcm}} = 5.0 + 0.04 \cdot x_{\text{ins, mm}} + m_{\text{pcm}} \quad [\text{kg}]$$
   * MATLAB: `w_total = W_base_can + w_ins + m_pcm;` (`optimizeDesign.m`, L54)

2. **System Cost Model [DESIGN ASSUMPTION]:**
   $$C_{\text{total}} = C_{\text{base}} + c_{\text{ins}} + c_{\text{pcm}} = 2400 + (12,000 \cdot 1.0 \cdot x_{\text{ins, m}}) + 640 \cdot m_{\text{pcm}} = 2400 + 12 \cdot x_{\text{ins, mm}} + 640 \cdot m_{\text{pcm}} \quad [\text{₹ INR}]$$
   * MATLAB: `c_total = C_base_inr + c_ins + c_pcm_inr * m_pcm;` (`optimizeDesign.m`, L55)

3. **Constraint Penalty:**
   $$\text{Penalty} = \begin{cases} 10.0 \cdot (T_{\text{milk, 8h}} - 10.0) & \text{if } T_{\text{milk, 8h}} > 10.0\,^\circ\text{C} \\ 0.0 & \text{otherwise} \end{cases}$$
   * MATLAB: (`optimizeDesign.m`, L82-86)

4. **Multi-Objective Merit Function ($J$):**
   $$J = 0.40 \cdot \left(\frac{T_{\text{milk, 8h}}}{5.0}\right)^2 + 0.30 \cdot \left(\frac{W_{\text{total}}}{12.0}\right) + 0.30 \cdot \left(\frac{C_{\text{total}}}{6400.0}\right) + \text{Penalty}$$
   * MATLAB: `j_score = w_T * (t_8h / 5.0)^2 + w_W * (w_total / 12.0) + w_C * (c_total / 6400.0) + penalty_constraint;` (`optimizeDesign.m`, L88)

---

## 12. Claims We Should NOT Make

> [!CAUTION]
> During presentations or academic defenses, **DO NOT** make the following claims:
> 1. **Do NOT claim 3D CAD/CFD modeling:** The digital twin uses 1D/lumped 2D cross-sectional ODE equations, not 3D Finite Element (FEM) or CFD mesh solvers.
> 2. **Do NOT claim experimental hardware validation:** No physical prototype or temperature sensors are connected. All outputs are strictly **SIMULATION / PREDICTION**.
> 3. **Do NOT claim exact commercial PCM product specs:** Thermophysical properties ($c_p, L_{\text{pcm}}$) are design assumptions requiring manufacturer datasheet validation.
> 4. **Do NOT claim variable ambient conditions:** The model currently assumes constant $T_{\text{amb}} = 35\,^\circ\text{C}$.
